using System.Runtime.CompilerServices;
using WfpChatBotWebApp.Helpers;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class FluxImageService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : IAiImageService, IAiImageEditService
{
    public IAsyncEnumerable<byte[]> CreateImage(string prompt, CancellationToken cancellationToken = default) =>
        Generate(prompt, null, cancellationToken);

    public IAsyncEnumerable<byte[]> EditImage(string prompt, BinaryData sourceImage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Generate(prompt, ImageInput.ToReferenceImage(sourceImage), cancellationToken);
    }

    private async IAsyncEnumerable<byte[]> Generate(string prompt, string? referenceImage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var foundryUrl = configuration["FoundryUrl"];
        var apiKey = configuration["openAiKey"];
        var modelId = configuration["FluxModelName"];
        if (string.IsNullOrWhiteSpace(foundryUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(modelId))
            throw new InvalidOperationException("FoundryUrl, OpenAiKey, and FluxModelName must be configured.");

        var endpoint = FluxEndpoint.BuildUrl(foundryUrl, modelId);
        using var httpClient = httpClientFactory.CreateClient("Flux");

        var bytes = referenceImage is null
            ? await FluxClient.GenerateAsync(httpClient, endpoint, apiKey, modelId, prompt, cancellationToken)
            : await FluxClient.EditAsync(httpClient, endpoint, apiKey, modelId, prompt, referenceImage, cancellationToken);

        if (bytes is not { Length: > 0 })
            throw new InvalidOperationException("FLUX returned no image bytes.");

        yield return bytes;
    }
}

