namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IAiImageService
{
    IAsyncEnumerable<byte[]> CreateImage(
        string prompt,
        CancellationToken cancellationToken = default);
}
