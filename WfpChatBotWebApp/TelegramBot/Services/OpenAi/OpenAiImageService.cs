using System.Runtime.CompilerServices;
using OpenAI.Images;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiImageService
{
    IAsyncEnumerable<string> CreateImage(
        string prompt,
        int numOfImages = 1,
        CancellationToken cancellationToken = default);
}

public class OpenAiImageService(
    IOpenAiClientFactory openAiClientFactory)
    : IOpenAiImageService
{
    public async IAsyncEnumerable<string> CreateImage(
        string prompt,
        int numOfImages = 1,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            var res = await openAiClientFactory.ImageClient.GenerateImageAsync(prompt, imageGenerationOptions, cancellationToken);
            yield return res.Value.ImageUri.OriginalString;
        }
    }
}
