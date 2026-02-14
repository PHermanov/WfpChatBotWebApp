using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Builders;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Extensions;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

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
            var chatMessage = await CreateChatMessage(request, cancellationToken);

            messagesQueue.Enqueue(chatMessage);
        }

        var me = await GetMe(cancellationToken);

        var completionOptions = openAiChatToolsService.RegisterTools(new ChatCompletionOptions());

        var stream = openAiClientFactory.ChatClient.CompleteChatStreamingAsync(
            messages: messagesQueue.ToArray(),
            options: completionOptions,
            cancellationToken: cancellationToken);

        StringBuilder contentBuilder = new();
        StreamingChatToolCallsBuilder toolCallsBuilder = new();

        await foreach (var completion in stream)
        {
            foreach (var contentPart in completion.ContentUpdate)
            {
                contentBuilder.Append(contentPart.Text);

                yield return new OpenAiResponse
                {
                    ContentType = OpenAiContentType.Text,
                    Content = contentBuilder.ToString(),
                    ContentComplete = false
                };
            }

            foreach (var toolCallUpdate in completion.ToolCallUpdates)
            {
                toolCallsBuilder.Append(toolCallUpdate);
            }
        }

        if (contentBuilder.Length != 0)
        {
            yield return new OpenAiResponse
            {
                ContentType = OpenAiContentType.Text,
                Content = contentBuilder.ToString(),
                ContentComplete = true
            };
        }

        var toolCalls = toolCallsBuilder.Build();

        if (toolCalls.Count != 0)
        {
            var assistantMessage = new AssistantChatMessage(toolCalls) { ParticipantName = me.Id.ToString() };
            if (contentBuilder.Length != 0)
            {
                assistantMessage.Content.Add(ChatMessageContentPart.CreateTextPart(contentBuilder.ToString()));
            }

            messagesQueue.Enqueue(assistantMessage);

            foreach (var toolCall in toolCalls)
            {
                var tcm = new ToolChatMessage(toolCall.Id, string.Empty);
                messagesQueue.Enqueue(tcm);

                await foreach (var toolOutput in openAiChatToolsService.GetToolCallOutput(toolCall, cancellationToken))
                {
                    tcm.Content.Add(toolOutput.Content);

                    yield return toolOutput;
                }
            }
        }
        else
        {
            messagesQueue.Enqueue(
                new AssistantChatMessage(contentBuilder.ToString()) { ParticipantName = me.Id.ToString() });
        }
    }

    private User? _botUser;
    private async ValueTask<User> GetMe(CancellationToken cancellationToken) =>
        _botUser ??= await botClient.GetMe(cancellationToken);

    private async ValueTask<ChatMessage> CreateChatMessage(OpenAiRequest request, CancellationToken cancellationToken)
    {
        var me = await GetMe(cancellationToken);

        ChatMessage chatMessage = request.UserId == me.Id
            ? new AssistantChatMessage(request.MessageText ?? string.Empty) { ParticipantName = request.UserId.ToString() }
            : new UserChatMessage(request.MessageText ?? string.Empty) { ParticipantName = request.UserId.ToString() };

        if (request.Image is not null)
        {
            var mediaType = GetImageMediaType(request.Image);
            chatMessage.Content.Add(ChatMessageContentPart.CreateImagePart(request.Image, mediaType, ChatImageDetailLevel.High));
        }

        return chatMessage;
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

    private async Task<SystemChatMessage> CreateSystemMessage(long chatId, CancellationToken cancellationToken)
    {
        var chatUsers = await gameRepository.GetActiveUsersForChatAsync(chatId, cancellationToken);

        var chatUserInfos = await Task.WhenAll(chatUsers
            .Select(async (u, i) =>
            {
                var cm = await botClient.GetChatMember(new ChatId(chatId), u.UserId, cancellationToken);

                var userName = (cm.User.Username ?? cm.User.FirstName);

                return $"{i}. UserId: {cm.User.Id}; UserName: {userName}; FirstName: {cm.User.FirstName}; LastName: {cm.User.LastName ?? string.Empty};";
            }));

        var botUser = await GetMe(cancellationToken);

        var prompt = string.Format(options.Value.SystemPrompt, DateTime.Now.ToString("F", CultureInfo.InvariantCulture));

        return ChatMessage.CreateSystemMessage(
            content: $"""
            {prompt}.
            
            Telegram chat participants are:
            {string.Join(Environment.NewLine, chatUserInfos)}
            
            Your identifiers are: UserId: {botUser.Id}; UserName: {botUser.Username ?? string.Empty}; FirstName: {botUser.FirstName}; LastName: {botUser.LastName ?? string.Empty};
            """);
    }
}
