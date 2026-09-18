namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IAiImageEditService
{
    IAsyncEnumerable<byte[]> EditImage(
        string prompt,
        BinaryData sourceImage,
        CancellationToken cancellationToken = default);
}
