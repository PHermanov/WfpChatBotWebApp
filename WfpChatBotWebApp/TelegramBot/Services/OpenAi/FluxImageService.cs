using ElBruno.Text2Image;
using ElBruno.Text2Image.Foundry;
using System.Runtime.CompilerServices;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class FluxImageService(IConfiguration configuration) : IAiImageService
{
    public async IAsyncEnumerable<(string?, byte[]?)> CreateImage(string prompt, int numOfImages = 1, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var options = new ImageGenerationOptions()
        {
            Height = 1024,
            Width = 1024
        };

        using var generator = new Flux2Generator(
            endpoint: configuration["FoundryUrl"]!,
            apiKey: configuration["openAiKey"]!,
            modelId: configuration["FluxModelName"]);

        for (var i = 0; i < numOfImages; i++)
        {
            var res = await generator.GenerateAsync(prompt, options, cancellationToken);
            yield return (null, res.ImageBytes);
        }
    }
}
