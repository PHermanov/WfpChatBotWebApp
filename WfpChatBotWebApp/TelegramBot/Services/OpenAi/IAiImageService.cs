namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IAiImageService
{
    IAsyncEnumerable<(string?, byte[]?)> CreateImage(
        string prompt,
        int numOfImages = 1,
        CancellationToken cancellationToken = default);
}
