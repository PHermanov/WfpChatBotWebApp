using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using OpenAI.Responses;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;
using System.Collections.Concurrent;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiChatService
{
    IAsyncEnumerable<OpenAiResponse> ProcessMessage(
        string threadKey,
        long chatId,
        OpenAiRequest[] requests,
        CancellationToken cancellationToken,
        ImageToolContext? imageContext = null);
}

public class OpenAiChatService(
    AIProjectClient projectClient,
    AgentReference agentReference,
    IOpenAiChatToolsService openAiChatToolsService,
    IGameRepository gameRepository,
    ITelegramBotClient botClient,
    IConversationStore conversationStore,
    ILogger<OpenAiChatService> logger)
    : IOpenAiChatService
{
    private const string FailedToolOutput = "The tool call failed and produced no result.";
    private const string ImagesAttachedNote = "The resulting image is attached after this output, labeled with its Telegram MessageId.";
    private const string ImagesUnavailableNote = "The resulting image could not be added to this conversation, so you cannot see it. If asked about it, say that it is unavailable and do not describe it from the prompt.";
    private const string ImageNotDeliveredOutput = "The image was generated, but sending it to the Telegram chat failed, so neither you nor the user can see it. If asked about it, say that it is unavailable and do not describe it from the prompt.";
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _conversationLocks = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _creationLock = new(1, 1);

    public async IAsyncEnumerable<OpenAiResponse> ProcessMessage(
        string threadKey,
        long chatId,
        OpenAiRequest[] requests,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ImageToolContext? imageContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadKey);
        imageContext ??= new ImageToolContext(requests.LastOrDefault(request => request.Image is not null)?.Image);
        var conversationsClient = projectClient.OpenAI.GetProjectConversationsClient();
        var conversationId = await GetOrCreateConversationId(threadKey, chatId, conversationsClient, cancellationToken);
        var conversationLock = _conversationLocks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));

        await conversationLock.WaitAsync(cancellationToken);

        // Every function call stored in the conversation needs an output, otherwise later turns are rejected.
        HashSet<string> unansweredCallIds = new(StringComparer.Ordinal);
        var completed = false;
        try
        {
            List<ResponseItem> inputItems = [];
            foreach (var request in requests)
            {
                inputItems.AddRange(await CreateChatMessages(request, cancellationToken));
            }

            // Tools (web search, CreateImage, EditImage) are declared on the agent definition;
            // the service rejects per-request tools when an agent is referenced.
            var responseOptions = new CreateResponseOptions
            {
                StreamingEnabled = true,
                TruncationMode = ResponseTruncationMode.Auto
            };

            foreach (var inputItem in inputItems)
            {
                responseOptions.InputItems.Add(inputItem);
            }

            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                agentReference,
                defaultConversationId: conversationId);
            var stream = responsesClient.CreateResponseStreamingAsync(responseOptions, cancellationToken);
            StringBuilder contentBuilder = new();
            ResponseResult? completedResponse = null;

            await foreach (var update in stream)
            {
                logger.LogDebug("Foundry stream update: {UpdateType}", update.GetType().Name);
                string? delta = null;
                switch (update)
                {
                    case StreamingResponseOutputTextDeltaUpdate textUpdate:
                        delta = textUpdate.Delta;
                        break;
                    case StreamingResponseRefusalDeltaUpdate refusalUpdate:
                        delta = refusalUpdate.Delta;
                        break;
                    case StreamingResponseOutputItemDoneUpdate { Item: FunctionCallResponseItem functionCall }:
                        unansweredCallIds.Add(functionCall.CallId);
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

            var messageItems = completedResponse.OutputItems
                .OfType<MessageResponseItem>()
                .ToArray();
            var finalContent = string.Concat(messageItems
                .SelectMany(message => message.Content)
                .Select(part => part.Kind == ResponseContentPartKind.Refusal ? part.Refusal : part.Text));
            finalContent = WebCitationFormatter.AppendSources(finalContent, messageItems);
            var toolCalls = completedResponse.OutputItems.OfType<FunctionCallResponseItem>().ToArray();
            unansweredCallIds.UnionWith(toolCalls.Select(toolCall => toolCall.CallId));
            if (finalContent.Length == 0 && toolCalls.Length == 0)
                throw new InvalidOperationException("OpenAI response contained no text or function calls.");

            logger.LogInformation(
                "Foundry response {ResponseId} for conversation {ConversationId} (thread {ThreadKey}) with {OutputItems} output items and {ToolCalls} tool calls",
                completedResponse.Id,
                conversationId,
                threadKey,
                completedResponse.OutputItems.Count,
                toolCalls.Length);

            if (finalContent.Length != 0)
            {
                yield return new OpenAiResponse
                {
                    ContentType = OpenAiContentType.Text,
                    Content = finalContent,
                    ContentComplete = true
                };
            }

            foreach (var toolCall in toolCalls)
            {
                logger.LogInformation(
                    "Foundry tool call {FunctionName} ({CallId}) for conversation {ConversationId}",
                    toolCall.FunctionName,
                    toolCall.CallId,
                    conversationId);

                StringBuilder toolResult = new();
                List<ResponseItem> resultImages = [];
                await foreach (var toolOutput in openAiChatToolsService.GetToolCallOutput(toolCall, cancellationToken, imageContext))
                {
                    yield return toolOutput;

                    if (toolOutput.ContentType != OpenAiContentType.ImageBytes)
                    {
                        toolResult.AppendLine(toolOutput.Content);
                        continue;
                    }

                    // DeliveredMessageId is set by the consumer while this iterator was suspended at the yield above.
                    if (toolOutput.DeliveredMessageId is not { } deliveredMessageId)
                    {
                        toolResult.AppendLine(ImageNotDeliveredOutput);
                        continue;
                    }

                    var label = DescribeImageResult(toolCall, deliveredMessageId, imageContext);
                    toolResult.AppendLine($"{label}.");
                    resultImages.Add(ResponseItem.CreateUserMessageItem([
                        ResponseContentPart.CreateInputTextPart($"Image of the {label}."),
                        CreateImagePart(BinaryData.FromBytes(toolOutput.ImageContent!))]));
                }

                if (toolResult.Length == 0)
                    throw new InvalidOperationException($"OpenAI tool '{toolCall.FunctionName}' returned no output.");

                await AppendToolOutput(conversationsClient, conversationId, toolCall.CallId, toolResult.ToString(), resultImages, cancellationToken);
                unansweredCallIds.Remove(toolCall.CallId);
            }

            logger.LogDebug(
                "Foundry response usage for conversation {ConversationId}: input={InputTokens}, output={OutputTokens}, total={TotalTokens}",
                conversationId,
                completedResponse.Usage?.InputTokenCount,
                completedResponse.Usage?.OutputTokenCount,
                completedResponse.Usage?.TotalTokenCount);

            completed = true;
        }
        finally
        {
            if (!completed)
            {
                conversationStore.Remove(threadKey);
                await AnswerPendingCalls(conversationsClient, conversationId, unansweredCallIds);
            }

            conversationLock.Release();
        }
    }

    private User? _botUser;
    private async ValueTask<User> GetMe(CancellationToken cancellationToken) =>
        _botUser ??= await botClient.GetMe(cancellationToken);

    private async ValueTask<ResponseItem[]> CreateChatMessages(OpenAiRequest request, CancellationToken cancellationToken)
    {
        var me = await GetMe(cancellationToken);
        var isAssistant = request.UserId == me.Id;
        var userId = request.UserId?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        var messageId = request.MessageId is { } id ? $"; MessageId: {id.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var replyTo = request.ReplyToMessageId is { } replyId ? $"; ReplyToMessageId: {replyId.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var text = $"Telegram UserId: {userId}{messageId}{replyTo}; Time: {DateTimeOffset.UtcNow:O}\n{request.MessageText ?? string.Empty}";

        if (request.Image is null)
        {
            return [isAssistant
                ? ResponseItem.CreateAssistantMessageItem(text)
                : ResponseItem.CreateUserMessageItem(text)];
        }

        var imagePart = CreateImagePart(request.Image);

        // Responses accepts input images on user messages, not assistant output messages.
        return isAssistant
            ? [ResponseItem.CreateAssistantMessageItem(text),
                ResponseItem.CreateUserMessageItem([
                    ResponseContentPart.CreateInputTextPart($"Image attached to the preceding message from Telegram UserId: {userId}{messageId}."),
                    imagePart])]
            : [ResponseItem.CreateUserMessageItem([ResponseContentPart.CreateInputTextPart(text), imagePart])];
    }

    private static ResponseContentPart CreateImagePart(BinaryData image)
    {
        var mediaType = ImageInput.GetMediaType(image) ?? "image/jpeg";
        var imageUri = new Uri($"data:{mediaType};base64,{Convert.ToBase64String(image.ToMemory().Span)}");
        return ResponseContentPart.CreateInputImagePart(imageUri, ResponseImageDetailLevel.High);
    }

    private static string DescribeImageResult(FunctionCallResponseItem toolCall, int deliveredMessageId, ImageToolContext imageContext)
    {
        var source = toolCall.FunctionName == nameof(IAiImageEditService.EditImage) && imageContext.SourceMessageId is { } sourceMessageId
            ? $", edited from the image in Telegram MessageId: {sourceMessageId.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"{toolCall.FunctionName} result (call {toolCall.CallId}) that you sent to the Telegram chat as MessageId: {deliveredMessageId.ToString(CultureInfo.InvariantCulture)}{source}";
    }

    // Result images go right after the function_call_output; if they cannot be stored, the output alone keeps the conversation valid.
    private async Task AppendToolOutput(
        ProjectConversationsClient conversationsClient,
        string conversationId,
        string callId,
        string output,
        IReadOnlyList<ResponseItem> resultImages,
        CancellationToken cancellationToken)
    {
        if (resultImages.Count > 0)
        {
            try
            {
                await conversationsClient.CreateProjectConversationItemsAsync(
                    conversationId,
                    [ResponseItem.CreateFunctionCallOutputItem(callId, output + ImagesAttachedNote), .. resultImages],
                    cancellationToken: cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                logger.LogWarning(
                    "Failed to attach {ImageCount} result images to conversation {ConversationId}: {ErrorType}",
                    resultImages.Count,
                    conversationId,
                    e.GetType().Name);
                output += ImagesUnavailableNote;
            }
        }

        await conversationsClient.CreateProjectConversationItemsAsync(
            conversationId,
            [ResponseItem.CreateFunctionCallOutputItem(callId, output)],
            cancellationToken: cancellationToken);
    }

    private async Task<string> GetOrCreateConversationId(
        string threadKey,
        long chatId,
        ProjectConversationsClient conversationsClient,
        CancellationToken cancellationToken)
    {
        if (conversationStore.TryGetConversationId(threadKey, out var conversationId))
            return conversationId;

        await _creationLock.WaitAsync(cancellationToken);
        try
        {
            if (conversationStore.TryGetConversationId(threadKey, out conversationId))
                return conversationId;

            var options = new ProjectConversationCreationOptions();
            options.Metadata["telegram_chat_id"] = chatId.ToString(CultureInfo.InvariantCulture);
            options.Metadata["telegram_thread_key"] = threadKey;
            options.Items.Add(await CreateContextMessage(chatId, cancellationToken));

            var created = (await conversationsClient.CreateProjectConversationAsync(options, cancellationToken)).Value;
            conversationStore.SetConversationId(threadKey, created.Id);
            logger.LogInformation("Created Foundry conversation {ConversationId} for thread {ThreadKey}", created.Id, threadKey);

            return created.Id;
        }
        finally
        {
            _creationLock.Release();
        }
    }

    private async Task AnswerPendingCalls(
        ProjectConversationsClient conversationsClient,
        string conversationId,
        IReadOnlyCollection<string> callIds)
    {
        if (callIds.Count == 0)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var batch in callIds.Chunk(20))
            {
                await conversationsClient.CreateProjectConversationItemsAsync(
                    conversationId,
                    batch.Select(callId => ResponseItem.CreateFunctionCallOutputItem(callId, FailedToolOutput)),
                    cancellationToken: timeout.Token);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(
                "Failed to close {CallCount} pending tool calls in conversation {ConversationId}: {ErrorType}",
                callIds.Count,
                conversationId,
                e.GetType().Name);
        }
    }

    private async Task<MessageResponseItem> CreateContextMessage(long chatId, CancellationToken cancellationToken)
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

        return ResponseItem.CreateSystemMessageItem(
            inputTextContent: $"""
            Context timestamp (UTC): {DateTimeOffset.UtcNow:O}.

            Telegram chat participants are:
            {string.Join(Environment.NewLine, chatUserInfos)}
            
            Your identifiers are: UserId: {botUser.Id}; UserName: {botUser.Username ?? string.Empty}; FirstName: {botUser.FirstName}; LastName: {botUser.LastName ?? string.Empty};
            """);
    }
}
