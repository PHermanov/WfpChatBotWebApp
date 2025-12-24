using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using OpenAI.Chat;
using OpenAI.Images;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Audio;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IOpenAiService
{
    IAsyncEnumerable<string> ProcessMessage(Guid contextKey, Message message, List<string> requests, List<BinaryData> images, CancellationToken cancellationToken);
    IAsyncEnumerable<string> CreateImage(string message, int numOfImages, CancellationToken cancellationToken);
    Task<string> ProcessAudio(Stream audioStream, CancellationToken cancellationToken);
}

public class OpenAiService : IOpenAiService
{
    private readonly ChatClient _chatClient;
    private readonly ImageClient _imageClient;
    private readonly AudioClient _audioClient;
    private readonly IGameRepository _gameRepository;
    private readonly ITelegramBotClient _botClient;

    private readonly string _systemPrompt;
    
    private readonly Dictionary<Guid, ChatMessageQueue> _messageQueues = new();

    public OpenAiService(
        IConfiguration config,
        IGameRepository gameRepository,
        ITelegramBotClient botClient)
    {
        var openAiKey = config["OpenAiKey"] ?? string.Empty;
        var openAiUrl = config["OpenAiUrl"] ?? string.Empty;

        var azureClient = new AzureOpenAIClient(
            new Uri(openAiUrl),
            new AzureKeyCredential(openAiKey));

        _chatClient = azureClient.GetChatClient(config["OpenAiChatModelName"]);
        _imageClient = azureClient.GetImageClient(config["OpenAiImageModelName"]);
        _audioClient = azureClient.GetAudioClient(config["OpenAiAudioModelName"]);
        _gameRepository = gameRepository;
        _botClient = botClient;

        _systemPrompt = config["SystemPrompt"] ?? string.Empty;
    }

    public async IAsyncEnumerable<string> ProcessMessage(
        Guid contextKey,
        Message message,
        List<string> requests,
        List<BinaryData> images,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messagesQueue = await GetChatMessageQueue(contextKey, message.Chat.Id, cancellationToken);

        var userChatMessage = new UserChatMessage()
        {
            ParticipantName = message.From?.Id.ToString()
        };
        
        foreach (var request in requests)
            userChatMessage.Content.Add(ChatMessageContentPart.CreateTextPart(request));

        if (images is { Count: > 0 })
        {
            foreach (var image in images)
                userChatMessage.Content.Add(ChatMessageContentPart.CreateImagePart(image, "image/jpeg", ChatImageDetailLevel.High));
        }

        messagesQueue.Enqueue(userChatMessage);

        var responseBuffer = new StringBuilder();

        var stream = _chatClient.CompleteChatStreamingAsync(
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
       
        messagesQueue.Enqueue(ChatMessage.CreateAssistantMessage(responseBuffer.ToString()));
    }

    public async IAsyncEnumerable<string> CreateImage(string message, int numOfImages = 1, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var options = new ImageGenerationOptions
        {
            Quality = GeneratedImageQuality.High,
            Size = GeneratedImageSize.W1024xH1024,
            Style = GeneratedImageStyle.Natural,
            ResponseFormat = GeneratedImageFormat.Uri
        };

        for (var i = 0; i < numOfImages; i++)
        {
            var res = await _imageClient.GenerateImageAsync(message, options, cancellationToken);
            yield return res.Value.ImageUri.OriginalString;
        }
    }

    public async Task<string> ProcessAudio(Stream audioStream, CancellationToken cancellationToken)
    {
        audioStream.Position = 0;
        var options = new AudioTranscriptionOptions { Language = "uk" };

        var audioTranscriptionResult = await _audioClient.TranscribeAudioAsync(audioStream, "voice.wav", options, cancellationToken);
        return audioTranscriptionResult.Value.Text;
    }

    private async ValueTask<ChatMessageQueue> GetChatMessageQueue(Guid contextKey, long chatId, CancellationToken cancellationToken)
    {
        if (_messageQueues.TryGetValue(contextKey, out var messagesQueue))
            return messagesQueue;
        
        var systemMessage = ChatMessage.CreateSystemMessage(await BuildInitialPrompt(chatId, cancellationToken));
        
        messagesQueue = new ChatMessageQueue();
        messagesQueue.Enqueue(systemMessage);
            
        _messageQueues.Add(contextKey, messagesQueue);

        return messagesQueue;
    }

    private async Task<string> BuildInitialPrompt(long chatId, CancellationToken cancellationToken)
    {
        var chatUsers = await _gameRepository.GetActiveUsersForChatAsync(chatId, cancellationToken);

        var chatUserInfos = await Task.WhenAll(chatUsers
            .Select(async (u, i) =>
            {
                var cm = await _botClient.GetChatMember(new ChatId(chatId), u.UserId, cancellationToken);

                return $"{i}. UserId: {u.UserId}; UserName: {u.UserName}; FirstName: {cm.User.FirstName}; LastName: {cm.User.LastName};";
            }));
        
        var prompt = string.Format(_systemPrompt, DateTime.Today);

        return $"{prompt}. Telegram chat participants are: {string.Join(" ", chatUserInfos)}";
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