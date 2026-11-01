using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services.Instagram;

public interface IInstagramService
{
    bool ContainsInstagramUrl(Message message);
    Task TrySendMediaAsync(Message message, CancellationToken cancellationToken);
}

public class InstagramService(
    ITelegramBotClient botClient,
    IHttpClientFactory httpClientFactory,
    ILogger<InstagramService> logger)
    : IInstagramService
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    private const string ShareResolverUserAgent = "curl/8.5.0";
    private const long MaxVideoBytes = 50L * 1024 * 1024;
    private const long MaxPhotoBytes = 10L * 1024 * 1024;
    private const int MaxAlbumSize = 10;
    private const int MaxMediaRedirects = 5;

    public bool ContainsInstagramUrl(Message message) => InstagramLinkParser.FindLinks(message).Count > 0;

    public async Task TrySendMediaAsync(Message message, CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient("Instagram");
        HashSet<string> handled = [];

        foreach (var link in InstagramLinkParser.FindLinks(message))
        {
            try
            {
                var shortcode = await ResolveShortcodeAsync(httpClient, link, cancellationToken);
                if (shortcode is null || !handled.Add(shortcode))
                    continue;

                var media = await GetMediaAsync(httpClient, shortcode, cancellationToken);
                if (media.Count == 0)
                {
                    logger.LogWarning("Instagram: no downloadable media found for {Shortcode}", shortcode);
                    continue;
                }

                await SendMediaAsync(httpClient, message, shortcode, media, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Instagram: failed to reshare media from {Path}", link.AbsolutePath);
            }
        }
    }

    private async Task<string?> ResolveShortcodeAsync(HttpClient httpClient, Uri link, CancellationToken cancellationToken)
    {
        if (InstagramLinkParser.TryGetShortcode(link, out var shortcode))
            return shortcode;

        using var request = new HttpRequestMessage(HttpMethod.Get, link);
        request.Headers.TryAddWithoutValidation("User-Agent", ShareResolverUserAgent);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var location = response.Headers.Location;
        if (location is { IsAbsoluteUri: false })
            location = new Uri(link, location);

        if (location != null && InstagramLinkParser.TryGetShortcode(location, out shortcode))
            return shortcode;

        logger.LogWarning("Instagram: share link {Path} returned {StatusCode} without a post redirect", link.AbsolutePath, (int)response.StatusCode);
        return null;
    }

    private async Task<IReadOnlyList<InstagramMedia>> GetMediaAsync(HttpClient httpClient, string shortcode, CancellationToken cancellationToken)
    {
        var postPage = await TryGetPageAsync(httpClient, new Uri($"https://www.instagram.com/p/{shortcode}/"), cancellationToken);
        if (postPage != null)
        {
            var media = InstagramMediaParser.ParsePostPage(postPage, shortcode);
            if (media.Count > 0)
                return media;
        }

        var embedPage = await TryGetPageAsync(httpClient, new Uri($"https://www.instagram.com/p/{shortcode}/embed/captioned/"), cancellationToken);
        return embedPage is null ? [] : InstagramMediaParser.ParseEmbedPage(embedPage, shortcode);
    }

    private async Task<string?> TryGetPageAsync(HttpClient httpClient, Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogWarning("Instagram: {Path} returned {StatusCode}", uri.AbsolutePath, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Instagram: request to {Path} failed", uri.AbsolutePath);
        }

        return null;
    }

    private async Task SendMediaAsync(HttpClient httpClient, Message message, string shortcode,
        IReadOnlyList<InstagramMedia> media, CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var chunk in media.Chunk(MaxAlbumSize))
        {
            List<(InstagramMediaKind Kind, InputFileStream File)> files = [];
            try
            {
                foreach (var item in chunk)
                {
                    index++;
                    var content = await TryDownloadAsync(httpClient, item, cancellationToken);
                    if (content is null)
                        continue;

                    var extension = item.Kind == InstagramMediaKind.Video ? "mp4" : "jpg";
                    files.Add((item.Kind, InputFile.FromStream(content, $"instagram_{shortcode}_{index}.{extension}")));
                }

                if (files.Count == 1)
                {
                    var (kind, file) = files[0];
                    if (kind == InstagramMediaKind.Video)
                        await botClient.TrySendVideoAsync(message.Chat.Id, file, logger, message.MessageId, cancellationToken);
                    else
                        await botClient.TrySendPhotoAsync(logger, message.Chat.Id, file, ParseMode.None,
                            replyToMessageId: message.MessageId, cancellationToken: cancellationToken);
                }
                else if (files.Count > 1)
                {
                    await botClient.TrySendMediaGroupAsync(
                        chatId: message.Chat.Id,
                        media: files.Select(f => f.Kind == InstagramMediaKind.Video
                            ? (IAlbumInputMedia)new InputMediaVideo(f.File) { SupportsStreaming = true }
                            : new InputMediaPhoto(f.File)),
                        logger: logger,
                        replyToMessageId: message.MessageId,
                        cancellationToken: cancellationToken);
                }
            }
            finally
            {
                foreach (var (_, file) in files)
                    await file.Content.DisposeAsync();
            }
        }
    }

    private async Task<MemoryStream?> TryDownloadAsync(HttpClient httpClient, InstagramMedia media, CancellationToken cancellationToken)
    {
        var limit = media.Kind == InstagramMediaKind.Video ? MaxVideoBytes : MaxPhotoBytes;
        MemoryStream? content = null;
        try
        {
            using var response = await GetMediaResponseAsync(httpClient, media.Url, cancellationToken);
            if (response is null)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Instagram: {Kind} download returned {StatusCode}", media.Kind, (int)response.StatusCode);
                return null;
            }

            if (response.Content.Headers.ContentLength > limit)
            {
                logger.LogWarning("Instagram: skipped {Kind} of {Bytes} bytes; Telegram upload limit is {Limit} bytes",
                    media.Kind, response.Content.Headers.ContentLength, limit);
                return null;
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            content = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (content.Length + read > limit)
                {
                    logger.LogWarning("Instagram: skipped {Kind} exceeding the Telegram upload limit of {Limit} bytes", media.Kind, limit);
                    await content.DisposeAsync();
                    return null;
                }

                content.Write(buffer, 0, read);
            }

            if (content.Length == 0)
            {
                await content.DisposeAsync();
                return null;
            }

            content.Position = 0;
            return content;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (content != null)
                await content.DisposeAsync();
            throw;
        }
        catch (Exception exception)
        {
            if (content != null)
                await content.DisposeAsync();
            logger.LogWarning(exception, "Instagram: {Kind} download failed", media.Kind);
            return null;
        }
    }

    private async Task<HttpResponseMessage?> GetMediaResponseAsync(HttpClient httpClient, Uri uri, CancellationToken cancellationToken)
    {
        // Instagram's CDN redirects between edge hosts; follow those hops manually and refuse anything off-CDN.
        for (var redirect = 0; redirect <= MaxMediaRedirects; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is < 300 or > 399 || response.Headers.Location is null)
                return response;

            var location = response.Headers.Location;
            response.Dispose();
            uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
            if (!InstagramMediaParser.IsInstagramCdnUrl(uri))
            {
                logger.LogWarning("Instagram: refused media redirect to {Host}", uri.Host);
                return null;
            }
        }

        logger.LogWarning("Instagram: media download exceeded {MaxRedirects} redirects", MaxMediaRedirects);
        return null;
    }
}
