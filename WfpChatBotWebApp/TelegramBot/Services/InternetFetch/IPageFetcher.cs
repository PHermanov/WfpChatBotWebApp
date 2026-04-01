namespace WfpChatBotWebApp.TelegramBot.Services.InternetFetch;

public interface IPageFetcher
{
    Task<string> Fetch(string url, CancellationToken ct = default);
}
