using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot.Services.Instagram;

public static partial class InstagramLinkParser
{
    [GeneratedRegex(@"(?<![\w.-])(?:https?://)?(?:[a-z0-9-]+\.)*(?:instagram\.com|instagr\.am)/[^\s<>""']*", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^/(?!share/)(?:[a-z0-9._]+/)?(?:p|reels?|tv)/(?!audio/)(?<code>[a-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PostPathRegex();

    [GeneratedRegex(@"^/share/(?:(?:p|reels?|tv)/)?[a-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex SharePathRegex();

    public static IReadOnlyList<Uri> FindLinks(Message message)
    {
        var textLinks = LinkRegex().Matches(message.Text ?? string.Empty).Select(match => match.Value);
        var entityLinks = (message.Entities ?? [])
            .Where(entity => entity.Type == MessageEntityType.TextLink && !string.IsNullOrEmpty(entity.Url))
            .Select(entity => LinkRegex().Match(entity.Url!))
            .Where(match => match.Success)
            .Select(match => match.Value);

        List<Uri> links = [];
        foreach (var value in textLinks.Concat(entityLinks))
        {
            var url = value.Contains("://", StringComparison.Ordinal) ? value : $"https://{value}";
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (TryGetShortcode(uri, out _) || IsShareLink(uri))
                && !links.Contains(uri))
            {
                links.Add(uri);
            }
        }

        return links;
    }

    public static bool TryGetShortcode(Uri uri, [NotNullWhen(true)] out string? shortcode)
    {
        shortcode = null;
        if (!IsInstagramHost(uri))
            return false;

        var match = PostPathRegex().Match(uri.AbsolutePath);
        if (!match.Success)
            return false;

        shortcode = match.Groups["code"].Value;
        return true;
    }

    public static bool IsShareLink(Uri uri) =>
        IsInstagramHost(uri) && SharePathRegex().IsMatch(uri.AbsolutePath);

    private static bool IsInstagramHost(Uri uri) =>
        uri.Scheme is "http" or "https"
        && (uri.Host.Equals("instagram.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".instagram.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("instagr.am", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".instagr.am", StringComparison.OrdinalIgnoreCase));
}
