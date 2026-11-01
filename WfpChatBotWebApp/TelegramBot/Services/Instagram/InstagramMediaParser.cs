using System.Text.Json;
using System.Text.RegularExpressions;

namespace WfpChatBotWebApp.TelegramBot.Services.Instagram;

/// <summary>
/// Extracts downloadable media from Instagram's logged-out post page (mobile API shape:
/// media_type/video_versions/image_versions2/carousel_media) and the embed page fallback
/// (GraphQL shape: is_video/video_url/display_url/edge_sidecar_to_children).
/// </summary>
public static partial class InstagramMediaParser
{
    private const int CarouselMediaType = 8;
    private const int VideoMediaType = 2;

    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 256 };

    private static readonly string[] MediaProperties =
        ["media_type", "video_versions", "image_versions2", "carousel_media", "video_url", "display_url", "edge_sidecar_to_children"];

    [GeneratedRegex(@"<script\b[^>]*type=""application/json""[^>]*>(?<json>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex JsonScriptRegex();

    [GeneratedRegex(@"""contextJSON"":(?<value>""(?:[^""\\]|\\.)*"")", RegexOptions.Singleline)]
    private static partial Regex ContextJsonRegex();

    public static IReadOnlyList<InstagramMedia> ParsePostPage(string html, string shortcode)
    {
        foreach (Match script in JsonScriptRegex().Matches(html))
        {
            var json = script.Groups["json"].Value;
            if (!json.Contains($"\"{shortcode}\"", StringComparison.Ordinal))
                continue;

            var media = ParseJson(json, shortcode);
            if (media.Count > 0)
                return media;
        }

        return [];
    }

    public static IReadOnlyList<InstagramMedia> ParseEmbedPage(string html, string shortcode)
    {
        foreach (Match match in ContextJsonRegex().Matches(html))
        {
            string? context;
            try
            {
                context = JsonSerializer.Deserialize<string>(match.Groups["value"].Value);
            }
            catch (JsonException)
            {
                continue;
            }

            if (string.IsNullOrEmpty(context))
                continue;

            var media = ParseJson(context, shortcode);
            if (media.Count > 0)
                return media;
        }

        return [];
    }

    private static IReadOnlyList<InstagramMedia> ParseJson(string json, string shortcode)
    {
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            foreach (var candidate in FindCandidates(document.RootElement, shortcode))
            {
                var media = ReadMedia(candidate);
                if (media.Count > 0)
                    return media;
            }
        }
        catch (JsonException)
        {
        }

        return [];
    }

    private static IEnumerable<JsonElement> FindCandidates(JsonElement element, string shortcode)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (GetString(element, "code") == shortcode || GetString(element, "shortcode") == shortcode)
            {
                if (element.TryGetProperty("if_not_gated_logged_out", out var gated) && HasMedia(gated))
                    yield return gated;

                if (HasMedia(element))
                    yield return element;
            }

            foreach (var property in element.EnumerateObject())
            foreach (var candidate in FindCandidates(property.Value, shortcode))
                yield return candidate;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            foreach (var candidate in FindCandidates(item, shortcode))
                yield return candidate;
        }
    }

    private static List<InstagramMedia> ReadMedia(JsonElement media)
    {
        if (TryGetArray(media, "carousel_media", out var carousel))
            return carousel.EnumerateArray().Select(ReadItem).OfType<InstagramMedia>().ToList();

        if (media.TryGetProperty("edge_sidecar_to_children", out var sidecar) && TryGetArray(sidecar, "edges", out var edges))
        {
            return edges.EnumerateArray()
                .Select(edge => edge.ValueKind == JsonValueKind.Object && edge.TryGetProperty("node", out var node) ? ReadItem(node) : null)
                .OfType<InstagramMedia>()
                .ToList();
        }

        // A carousel without its children would otherwise degrade to the cover image only.
        if (GetInt(media, "media_type") == CarouselMediaType
            || GetString(media, "__typename")?.EndsWith("Sidecar", StringComparison.Ordinal) == true)
            return [];

        return ReadItem(media) is { } item ? [item] : [];
    }

    private static InstagramMedia? ReadItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        var videoUrl = FirstUrl(item, "video_versions") ?? GetString(item, "video_url");
        if (CreateMedia(InstagramMediaKind.Video, videoUrl) is { } video)
            return video;

        // Never substitute a video's thumbnail for the video itself.
        if (GetInt(item, "media_type") == VideoMediaType || item.TryGetProperty("is_video", out var isVideo) && isVideo.ValueKind == JsonValueKind.True)
            return null;

        var photoUrl = (item.TryGetProperty("image_versions2", out var images) ? FirstUrl(images, "candidates") : null)
                       ?? GetString(item, "display_url");
        return CreateMedia(InstagramMediaKind.Photo, photoUrl);
    }

    public static bool IsInstagramCdnUrl(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.EndsWith(".cdninstagram.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase));

    private static InstagramMedia? CreateMedia(InstagramMediaKind kind, string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsInstagramCdnUrl(uri)
            ? new InstagramMedia(kind, uri)
            : null;

    private static bool HasMedia(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && MediaProperties.Any(name => element.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null);

    private static string? FirstUrl(JsonElement element, string arrayName) =>
        TryGetArray(element, arrayName, out var array)
            ? array.EnumerateArray().Select(entry => GetString(entry, "url")).FirstOrDefault(url => !string.IsNullOrEmpty(url))
            : null;

    private static bool TryGetArray(JsonElement element, string name, out JsonElement array)
    {
        array = default;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(name, out array)
               && array.ValueKind == JsonValueKind.Array;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
                                                  && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
}
