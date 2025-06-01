using Flurl.Http;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Extensions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface ITikTokService
{
    bool ContainsTikTokUrl(Message message);
    Task TryDownloadVideo(Message message, CancellationToken cancellationToken);
}

public class TikTokService (ITelegramBotClient botClient, ILogger<TikTokService> logger)
    : ITikTokService
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36";
    
    public bool ContainsTikTokUrl(Message message) =>
        message
            .Text!
            .Split(" ", StringSplitOptions.RemoveEmptyEntries)
            .Any(p => p.Contains("tiktok.com"));
    
    public async Task TryDownloadVideo(Message message, CancellationToken cancellationToken)
    {
        try
        {
            var url = message
                .Text!
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(p => p.Contains("tiktok.com"));

            if (string.IsNullOrEmpty(url))
                return;
            
            var videoUrl = await GetVideoUrl(url, cancellationToken);

            if (string.IsNullOrEmpty(videoUrl))
                return;

            var video = await videoUrl
                .WithHeaders(new { User_Agent = UserAgent })
                .GetStreamAsync(cancellationToken: cancellationToken);

            await botClient.TrySendVideoAsync(
                chatId: message.Chat.Id,
                video: InputFile.FromStream(video),
                replyToMessageId: message.MessageId,
                logger: logger,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in {Class}", nameof(TryDownloadVideo));
        }
    }
    
    private static async Task<string?> GetVideoUrl(string url, CancellationToken cancellationToken)
    {
        var downloaderPageResponse = await "https://snaptik.pro/"
            .WithHeaders(new { User_Agent = UserAgent })
            .GetAsync(cancellationToken: cancellationToken);

        if (downloaderPageResponse == null)
            return string.Empty;
        
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(await downloaderPageResponse.GetStringAsync());

        var token = htmlDoc.DocumentNode.Descendants()
            .FirstOrDefault(e => e.Name == "input" && e.GetAttributeValue("name", string.Empty) == "token")
            ?.GetAttributeValue("value", string.Empty);

        if (token == null)
            return string.Empty;
        
        var downloadPageResponse = await "https://snaptik.pro/action"
            .WithHeaders(new { User_Agent = UserAgent })
            .WithCookies(downloaderPageResponse.Cookies)
            .PostMultipartAsync(mp => mp.AddStringParts(new { url, token, submit = "1" }), cancellationToken: cancellationToken);

        if (downloadPageResponse == null)
            return string.Empty;
        
        var downloadPageResponseStream = await downloadPageResponse.GetStringAsync();

        if (downloadPageResponseStream == null)
            return string.Empty;
            
        
        var downloadPageJson = JsonNode.Parse(downloadPageResponseStream);

        if (downloadPageJson?["error"]?.GetValue<bool>() == true)
            return string.Empty;
        
        htmlDoc = new HtmlDocument();

        if (downloadPageJson?["html"] != null)
            htmlDoc.LoadHtml(downloadPageJson["html"]!.ToString());
        else
            return string.Empty;
        
        return htmlDoc.DocumentNode.Descendants()
            .FirstOrDefault(e => e.Name == "a" && e.GetAttributeValue("href", string.Empty).Contains("tiktokcdn", StringComparison.OrdinalIgnoreCase))
            ?.GetAttributeValue("href", string.Empty);
    }
}