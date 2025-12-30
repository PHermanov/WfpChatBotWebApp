using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiService
{
    IAsyncEnumerable<string> ProcessMessage(Guid contextKey, long chatId, List<OpenAiRequest> requests, CancellationToken cancellationToken);
    IAsyncEnumerable<string> CreateImage(string message, int numOfImages, CancellationToken cancellationToken);
    Task<string> ProcessAudio(Stream audioStream, CancellationToken cancellationToken);
}

public class OpenAiService(
    IOptions<OpenAiServiceOptions> options,
    IGameRepository gameRepository,
    ITelegramBotClient botClient)
    : IOpenAiService
{
    private readonly Dictionary<Guid, ChatMessageQueue> _messageQueues = new();

    [field: MaybeNull]
    private AzureOpenAIClient AzureOpenAiClient => field ??= new AzureOpenAIClient(
        new Uri(options.Value.OpenAiUrl),
        new AzureKeyCredential(options.Value.OpenAiKey));
    
    [field: MaybeNull]
    private ChatClient ChatClient => field ??= AzureOpenAiClient
        .GetChatClient(options.Value.OpenAiChatModelName);

    [field: MaybeNull]
    private ImageClient ImageClient => field ??= AzureOpenAiClient
        .GetImageClient(options.Value.OpenAiImageModelName);
    
    [field: MaybeNull]
    private AudioClient AudioClient => field ??= AzureOpenAiClient
        .GetAudioClient(options.Value.OpenAiAudioModelName);

    public async IAsyncEnumerable<string> ProcessMessage(
        Guid contextKey,
        long chatId,
        List<OpenAiRequest> requests,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messagesQueue = await GetChatMessageQueue(contextKey, chatId, cancellationToken);

        foreach (var request in requests)
        {
            var chatMessage = await CreateChatMessage(request, cancellationToken);

            messagesQueue.Enqueue(chatMessage);
        }

        var responseBuffer = new StringBuilder();

        var stream = ChatClient.CompleteChatStreamingAsync(
            messagesQueue.ToArray(),
            cancellationToken: cancellationToken);

        await foreach (var completion in stream)
        {
            foreach (var contentPart in completion.ContentUpdate)
            {
                responseBuffer.Append(contentPart.Text);
                yield return contentPart.Text;
            }
        }
        var me = await GetMe(cancellationToken);
        
        messagesQueue.Enqueue(
            new AssistantChatMessage(responseBuffer.ToString()) { ParticipantName = me.Id.ToString() });
    }

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

    private User? _botUser;
    private async ValueTask<User> GetMe(CancellationToken cancellationToken) =>
        _botUser ??= await botClient.GetMe(cancellationToken);

    public async IAsyncEnumerable<string> CreateImage(string message, int numOfImages = 1, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var imageGenerationOptions = new ImageGenerationOptions
        {
            Quality = GeneratedImageQuality.High,
            Size = GeneratedImageSize.W1024xH1024,
            Style = GeneratedImageStyle.Natural,
            ResponseFormat = GeneratedImageFormat.Uri
        };

        for (var i = 0; i < numOfImages; i++)
        {
            var res = await ImageClient.GenerateImageAsync(message, imageGenerationOptions, cancellationToken);
            yield return res.Value.ImageUri.OriginalString;
        }
    }

    public async Task<string> ProcessAudio(Stream audioStream, CancellationToken cancellationToken)
    {
        audioStream.Position = 0;
        var audioTranscriptionOptions = new AudioTranscriptionOptions { Language = "uk" };

        var audioTranscriptionResult = await AudioClient.TranscribeAudioAsync(audioStream, "voice.wav", audioTranscriptionOptions, cancellationToken);
        return audioTranscriptionResult.Value.Text;
    }

    private async ValueTask<ChatMessageQueue> GetChatMessageQueue(Guid contextKey, long chatId, CancellationToken cancellationToken)
    {
        if (_messageQueues.TryGetValue(contextKey, out var messagesQueue))
            return messagesQueue;
        
        messagesQueue = new ChatMessageQueue();
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
        
        var prompt = string.Format(options.Value.SystemPrompt, DateTime.Today);

        return ChatMessage.CreateSystemMessage( 
            content: $"""
            {prompt}.
            
            Telegram chat participants are:
            {string.Join(Environment.NewLine, chatUserInfos)}
            
            Your identifiers are: UserId: {botUser.Id}; UserName: {botUser.Username?.EscapeMarkdownString()}; FirstName: {botUser.FirstName.EscapeMarkdownString()}; LastName: {botUser.LastName?.EscapeMarkdownString()};
            """);
    }
}

public class ChatMessageQueue
{
    private readonly ConcurrentQueue<ChatMessage> _internalQueue = new();
    private readonly Lock _lockObject = new();

    public void Enqueue(ChatMessage obj)
    {
        lock (_lockObject)
        {
            _internalQueue.Enqueue(obj);
        }
    }
    
    public ChatMessage[] ToArray()
    {
        lock (_lockObject)
        {
            return _internalQueue.ToArray();
        }
    }
}