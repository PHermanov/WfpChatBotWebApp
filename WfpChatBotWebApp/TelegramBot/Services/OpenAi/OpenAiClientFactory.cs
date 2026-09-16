using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Images;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiClientFactory
{
    ResponsesClient ResponsesClient { get; }
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
    public ResponsesClient ResponsesClient => field ??= new ResponsesClient(
        new ApiKeyCredential(options.Value.OpenAiKey),
        new OpenAIClientOptions { Endpoint = GetResponsesEndpoint() });

    private Uri GetResponsesEndpoint()
    {
        var endpoint = new UriBuilder(options.Value.OpenAiUrl);
        var path = endpoint.Path.TrimEnd('/');
        if (!path.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            path += path.EndsWith("/openai", StringComparison.OrdinalIgnoreCase)
                ? "/v1"
                : "/openai/v1";
        }

        endpoint.Path = path;
        return endpoint.Uri;
    }

    [field: MaybeNull]
    public ImageClient ImageClient => field ??= AzureOpenAiClient
        .GetImageClient(options.Value.OpenAiImageModelName);
    
    [field: MaybeNull]
    public AudioClient AudioClient => field ??= AzureOpenAiClient
        .GetAudioClient(options.Value.OpenAiAudioModelName);
}
