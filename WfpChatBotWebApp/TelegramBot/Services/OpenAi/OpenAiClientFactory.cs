using System.Diagnostics.CodeAnalysis;
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

public class OpenAiClientFactory(
    IOptions<OpenAiClientFactoryOptions> options)
    : IOpenAiClientFactory
{
    [field: MaybeNull]
    private AzureOpenAIClient AzureOpenAiClient => field ??= new AzureOpenAIClient(
        new Uri(options.Value.OpenAiUrl),
        new AzureKeyCredential(options.Value.OpenAiKey));
    
    [field: MaybeNull]
    public ChatClient ChatClient => field ??= AzureOpenAiClient
        .GetChatClient(options.Value.OpenAiChatModelName);

    [field: MaybeNull]
    public ImageClient ImageClient => field ??= AzureOpenAiClient
        .GetImageClient(options.Value.OpenAiImageModelName);
    
    [field: MaybeNull]
    public AudioClient AudioClient => field ??= AzureOpenAiClient
        .GetAudioClient(options.Value.OpenAiAudioModelName);
}
