using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
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
            chatMessage.Content.Add(
                ChatMessageContentPart.CreateImagePart(request.Image, "image/jpeg", ChatImageDetailLevel.High));
        }

        return chatMessage;
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

                var userName = (cm.User.Username ?? cm.User.FirstName).EscapeMarkdownString();
                
                return $"{i}. UserId: {cm.User.Id}; UserName: {userName}; FirstName: {cm.User.FirstName.EscapeMarkdownString()}; LastName: {cm.User.LastName?.EscapeMarkdownString()};";
            }));
        
        var botUser = await GetMe(cancellationToken);
        
        var prompt = string.Format(options.Value.SystemPrompt, DateTime.Now.ToString("F", CultureInfo.InvariantCulture));

        return ChatMessage.CreateSystemMessage( 
            content: $"""
            {prompt}.
            
            Telegram chat participants are:
            {string.Join(Environment.NewLine, chatUserInfos)}
            
            Your identifiers are: UserId: {botUser.Id}; UserName: {botUser.Username?.EscapeMarkdownString()}; FirstName: {botUser.FirstName.EscapeMarkdownString()}; LastName: {botUser.LastName?.EscapeMarkdownString()};
            """);
    }
}
