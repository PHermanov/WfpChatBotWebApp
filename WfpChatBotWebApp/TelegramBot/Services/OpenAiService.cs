using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using OpenAI.Chat;
using OpenAI.Images;
using AI.Dev.OpenAI.GPT;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Audio;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IOpenAiService
{
    IAsyncEnumerable<string> ProcessMessage(Guid contextKey, string message, CancellationToken cancellationToken);
    IAsyncEnumerable<string> CreateImage(string message, int numOfImages, CancellationToken cancellationToken);
    Task<string> ProcessAudio(Stream audioStream, CancellationToken cancellationToken);
}

public class OpenAiService : IOpenAiService
{
    
    private readonly ChatClient _chatClient; 
    private readonly ImageClient _imageClient; 
    private readonly AudioClient _audioClient; 
    
    private readonly Dictionary<Guid, ChatMessageQueue> _messageQueues = new();
    
    public OpenAiService(ConfigurationManager config)
    {
        var openAiKey = config["OpenAiKey"] ?? string.Empty;
        var openAiUrl = config["OpenAiUrl"] ?? string.Empty;
        
        var azureClient = new AzureOpenAIClient(
            new Uri(openAiUrl),
            new AzureKeyCredential(openAiKey));
        
        _chatClient = azureClient.GetChatClient(config["OpenAiChatModelName"]);
        _imageClient = azureClient.GetImageClient(config["OpenAiImageModelName"]);
        _audioClient = azureClient.GetAudioClient(config["OpenAiAudioModelName"]);
    }
    
    public async IAsyncEnumerable<string> ProcessMessage(Guid contextKey, string message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_messageQueues.TryGetValue(contextKey, out var messagesQueue))
        {
            messagesQueue = new ChatMessageQueue(maxTokens: 150_000);
            _messageQueues.Add(contextKey, messagesQueue);
        }

        messagesQueue.Enqueue(ChatMessage.CreateUserMessage(message));

        var responseBuffer = new StringBuilder();

        var stream = _chatClient.CompleteChatStreamingAsync(
            messagesQueue.ToArray(),
            new ChatCompletionOptions { Temperature = 0.5f, ResponseFormat = ChatResponseFormat.Text},
            cancellationToken);

        await foreach (var completion in stream)
        {
            foreach (var contentPart in completion.ContentUpdate)
            {
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
            Style = GeneratedImageStyle.Vivid,
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
        var options = new AudioTranscriptionOptions { ResponseFormat = AudioTranscriptionFormat.Text };
        
        var audioTranscriptionResult = await _audioClient.TranscribeAudioAsync(audioStream, "voice_note.ogg", options, cancellationToken);
        return audioTranscriptionResult.Value.Text;
    }
}

public class ChatMessageQueue(int maxTokens)
{
    private readonly ConcurrentQueue<ChatMessage> _internalQueue = new();
    private readonly object _lockObject = new();

    public void Enqueue(ChatMessage obj)
    {
        lock (_lockObject)
        {
            _internalQueue.Enqueue(obj);
        }

        lock (_lockObject)
        {
            while (_internalQueue.ToArray().Sum(cm => GPT3Tokenizer.Encode(cm.Content.FirstOrDefault()?.Text!).Count) > maxTokens
                   && _internalQueue.TryDequeue(out _))
            { }
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