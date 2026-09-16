using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Extensions;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiChatService
{
    IAsyncEnumerable<OpenAiResponse> ProcessMessage(
        Guid contextKey,
        long chatId,
        OpenAiRequest[] requests,
        CancellationToken cancellationToken);
}

public class OpenAiChatService(
    IOptions<OpenAiChatServiceOptions> options,
    IOptions<OpenAiClientFactoryOptions> clientOptions,
    IOpenAiClientFactory openAiClientFactory,
    IOpenAiChatToolsService openAiChatToolsService,
    IGameRepository gameRepository,
    ITelegramBotClient botClient)
    : IOpenAiChatService
{
    private readonly Dictionary<Guid, OpenAiChatMessageQueue> _messageQueues = new();

    public async IAsyncEnumerable<OpenAiResponse> ProcessMessage(
        Guid contextKey,
        long chatId,
        OpenAiRequest[] requests,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messagesQueue = await GetChatMessageQueue(contextKey, chatId, cancellationToken);

        foreach (var request in requests)
        {
            messagesQueue.EnqueueRange(await CreateChatMessages(request, cancellationToken));
        }

        var responseOptions = openAiChatToolsService.RegisterTools(
            new CreateResponseOptions(clientOptions.Value.OpenAiChatModelName, messagesQueue.ToArray())
            {
                StreamingEnabled = true,
                StoredOutputEnabled = false,
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = ResponseReasoningEffortLevel.High
                },
                IncludedProperties = { IncludedResponseProperty.ReasoningEncryptedContent }
            });

        var stream = openAiClientFactory.ResponsesClient.CreateResponseStreamingAsync(responseOptions, cancellationToken);
        StringBuilder contentBuilder = new();
        ResponseResult? completedResponse = null;

        await foreach (var update in stream)
        {
            string? delta = null;
            switch (update)
            {
                case StreamingResponseOutputTextDeltaUpdate textUpdate:
                    delta = textUpdate.Delta;
                    break;
                case StreamingResponseRefusalDeltaUpdate refusalUpdate:
                    delta = refusalUpdate.Delta;
                    break;
                case StreamingResponseCompletedUpdate completedUpdate:
                    completedResponse = completedUpdate.Response;
                    break;
                case StreamingResponseFailedUpdate failedUpdate:
                    throw new InvalidOperationException($"OpenAI response failed: {failedUpdate.Response.Error?.Code}: {failedUpdate.Response.Error?.Message}");
                case StreamingResponseIncompleteUpdate incompleteUpdate:
                    throw new InvalidOperationException($"OpenAI response was incomplete: {incompleteUpdate.Response.IncompleteStatusDetails?.Reason}");
                case StreamingResponseErrorUpdate errorUpdate:
                    throw new InvalidOperationException($"OpenAI streaming error: {errorUpdate.Code}: {errorUpdate.Message}");
            }

            if (!string.IsNullOrEmpty(delta))
            {
                contentBuilder.Append(delta);
                yield return new OpenAiResponse
                {
                    ContentType = OpenAiContentType.Text,
                    Content = contentBuilder.ToString(),
                    ContentComplete = false
                };
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (completedResponse is null)
            throw new InvalidOperationException("OpenAI stream ended without a completed response.");

        var finalContent = string.Concat(completedResponse.OutputItems
            .OfType<MessageResponseItem>()
            .SelectMany(message => message.Content)
            .Select(part => part.Kind == ResponseContentPartKind.Refusal ? part.Refusal : part.Text));
        var toolCalls = completedResponse.OutputItems.OfType<FunctionCallResponseItem>().ToArray();
        if (finalContent.Length == 0 && toolCalls.Length == 0)
            throw new InvalidOperationException("OpenAI response contained no text or function calls.");

        if (finalContent.Length != 0)
        {
            yield return new OpenAiResponse
            {
                ContentType = OpenAiContentType.Text,
                Content = finalContent,
                ContentComplete = true
            };
        }

        // Replay all output items, including encrypted reasoning, but never retain an unanswered tool call.
        List<ResponseItem> outputItems = [.. completedResponse.OutputItems];
        foreach (var toolCall in toolCalls)
        {
            StringBuilder toolResult = new();
            await foreach (var toolOutput in openAiChatToolsService.GetToolCallOutput(toolCall, cancellationToken))
            {
                toolResult.AppendLine(toolOutput.ContentType == OpenAiContentType.ImageBytes
                    ? "Image generated and sent to the Telegram chat."
                    : toolOutput.Content);
                yield return toolOutput;
            }

            if (toolResult.Length == 0)
                throw new InvalidOperationException($"OpenAI tool '{toolCall.FunctionName}' returned no output.");

            outputItems.Add(ResponseItem.CreateFunctionCallOutputItem(toolCall.CallId, toolResult.ToString()));
        }

        cancellationToken.ThrowIfCancellationRequested();
        messagesQueue.EnqueueRange(outputItems);
    }

    private User? _botUser;
    private async ValueTask<User> GetMe(CancellationToken cancellationToken) =>
        _botUser ??= await botClient.GetMe(cancellationToken);

    private async ValueTask<ResponseItem[]> CreateChatMessages(OpenAiRequest request, CancellationToken cancellationToken)
    {
        var me = await GetMe(cancellationToken);
        var isAssistant = request.UserId == me.Id;
        var userId = request.UserId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        var text = $"Telegram UserId: {userId}\n{request.MessageText ?? string.Empty}";

        if (request.Image is null)
        {
            return [isAssistant
                ? ResponseItem.CreateAssistantMessageItem(text)
                : ResponseItem.CreateUserMessageItem(text)];
        }

        var mediaType = GetImageMediaType(request.Image);
        var imageUri = new Uri($"data:{mediaType};base64,{Convert.ToBase64String(request.Image.ToArray())}");
        var imagePart = ResponseContentPart.CreateInputImagePart(imageUri, ResponseImageDetailLevel.High);

        // Responses accepts input images on user messages, not assistant output messages.
        return isAssistant
            ? [ResponseItem.CreateAssistantMessageItem(text),
                ResponseItem.CreateUserMessageItem([
                    ResponseContentPart.CreateInputTextPart($"Image attached to the preceding message from Telegram UserId: {userId}."),
                    imagePart])]
            : [ResponseItem.CreateUserMessageItem([ResponseContentPart.CreateInputTextPart(text), imagePart])];
    }

    private static string GetImageMediaType(BinaryData image)
    {
        var bytes = image.ToArray();

        // WebP: "RIFF"...."WEBP" (bytes 0-3 == 'R','I','F','F' and bytes 8-11 == 'W','E','B','P')
        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return "image/webp";
        }

        // JPEG
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // PNG
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        // GIF
        if (bytes.Length >= 3 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F')
            return "image/gif";

        // BMP
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
            return "image/bmp";

        // AVIF (ftyp...avif)
        if (bytes.Length >= 12 &&
            bytes[4] == (byte)'f' && bytes[5] == (byte)'t' && bytes[6] == (byte)'y' && bytes[7] == (byte)'p' &&
            bytes[8] == (byte)'a' && bytes[9] == (byte)'v' && bytes[10] == (byte)'i' && bytes[11] == (byte)'f')
        {
            return "image/avif";
        }

        // Fallback: preserve previous behavior to avoid breaking existing callers.
        return "image/jpeg";
    }

    private async ValueTask<OpenAiChatMessageQueue> GetChatMessageQueue(Guid contextKey, long chatId, CancellationToken cancellationToken)
    {
        if (_messageQueues.TryGetValue(contextKey, out var messagesQueue))
            return messagesQueue;

        messagesQueue = new OpenAiChatMessageQueue();
        messagesQueue.Enqueue(await CreateSystemMessage(chatId, cancellationToken));

        _messageQueues.Add(contextKey, messagesQueue);

        return messagesQueue;
    }

    private async Task<MessageResponseItem> CreateSystemMessage(long chatId, CancellationToken cancellationToken)
    {
        var chatUsers = await gameRepository.GetActiveUsersForChatAsync(chatId, cancellationToken);

        var chatUserInfos = await Task.WhenAll(chatUsers
            .Select(async (u, i) =>
            {
                var cm = await botClient.GetChatMember(new ChatId(chatId), u.UserId, cancellationToken);

                var userName = cm.User.Username ?? cm.User.FirstName;

                return $"{i}. UserId: {cm.User.Id}; UserName: {userName}; FirstName: {cm.User.FirstName}; LastName: {cm.User.LastName ?? string.Empty};";
            }));

        var botUser = await GetMe(cancellationToken);

        var prompt = string.Format(options.Value.SystemPrompt, DateTime.Now.ToString("F", CultureInfo.InvariantCulture));

        return ResponseItem.CreateSystemMessageItem(
            inputTextContent: $"""
            {prompt}.

            Telegram chat participants are:
            {string.Join(Environment.NewLine, chatUserInfos)}
            
            Your identifiers are: UserId: {botUser.Id}; UserName: {botUser.Username ?? string.Empty}; FirstName: {botUser.FirstName}; LastName: {botUser.LastName ?? string.Empty};
            """);
    }
}
