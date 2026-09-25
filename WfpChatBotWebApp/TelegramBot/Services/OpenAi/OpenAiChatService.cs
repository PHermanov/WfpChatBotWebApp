using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Extensions;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiChatService
{
    IAsyncEnumerable<OpenAiResponse> ProcessMessage(
        Guid contextKey,
        long chatId,
        OpenAiRequest[] requests,
        CancellationToken cancellationToken,
        ImageToolContext? imageContext = null);
}

public class OpenAiChatService(
    ITextMessageService textMessageService,
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
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ImageToolContext? imageContext = null)
    {
        imageContext ??= new ImageToolContext(requests.LastOrDefault(request => request.Image is not null)?.Image);
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
            await foreach (var toolOutput in openAiChatToolsService.GetToolCallOutput(toolCall, cancellationToken, imageContext))
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

        var mediaType = ImageInput.GetMediaType(request.Image) ?? "image/jpeg";
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

        var promptTemplate = await textMessageService.GetMessageByNameAsync(Messages.SystemPrompt, cancellationToken);
        var prompt = string.Format(promptTemplate, DateTime.Now.ToString("F", CultureInfo.InvariantCulture));

        return ResponseItem.CreateSystemMessageItem(
            inputTextContent: $"""
            {prompt}.

            Telegram chat participants are:
            {string.Join(Environment.NewLine, chatUserInfos)}
            
            Your identifiers are: UserId: {botUser.Id}; UserName: {botUser.Username ?? string.Empty}; FirstName: {botUser.FirstName}; LastName: {botUser.LastName ?? string.Empty};
            """);
    }
}
