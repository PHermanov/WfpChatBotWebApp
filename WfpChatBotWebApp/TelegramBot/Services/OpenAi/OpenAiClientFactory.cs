using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiClientFactory
{
    ChatClient ChatClient { get; }
    ImageClient ImageClient { get; }
    AudioClient AudioClient { get; }
}

public class OpenAiClientFactory : IOpenAiClientFactory
{
    private readonly IOptions<OpenAiClientFactoryOptions> _options;
    private AzureOpenAIClient? _azureOpenAiClient;
    private ChatClient? _chatClient;
    private ImageClient? _imageClient;
    private AudioClient? _audioClient;

    public OpenAiClientFactory(IOptions<OpenAiClientFactoryOptions> options)
    {
        _options = options;
    }

    private AzureOpenAIClient AzureOpenAiClient =>
        _azureOpenAiClient ??= new AzureOpenAIClient(
            new Uri(_options.Value.OpenAiUrl),
            new AzureKeyCredential(_options.Value.OpenAiKey));

    public ChatClient ChatClient =>
        _chatClient ??= AzureOpenAiClient.GetChatClient(_options.Value.OpenAiChatModelName);

    public ImageClient ImageClient =>
        _imageClient ??= AzureOpenAiClient.GetImageClient(_options.Value.OpenAiImageModelName);

    public AudioClient AudioClient =>
        _audioClient ??= AzureOpenAiClient.GetAudioClient(_options.Value.OpenAiAudioModelName);
}
