using System.Net;
using System.Text;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public static class WebCitationFormatter
{
    private const int MaxSources = 5;

    public static string AppendSources(string content, IEnumerable<MessageResponseItem> messages)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var links = CollectLinks(messages).Take(MaxSources).ToArray();
        if (links.Length == 0)
            return content;

        var builder = new StringBuilder(content);
        builder.Append("\n\n");

        foreach (var (url, title) in links)
        {
            builder.Append("🔗 <a href=\"")
                .Append(WebUtility.HtmlEncode(url))
                .Append("\">")
                .Append(WebUtility.HtmlEncode(title))
                .Append("</a>\n");
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static IEnumerable<(string Url, string Title)> CollectLinks(IEnumerable<MessageResponseItem> messages)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var citation in messages
                     .SelectMany(message => message.Content)
                     .Where(part => part.Kind == ResponseContentPartKind.OutputText)
                     .SelectMany(part => part.OutputTextAnnotations ?? [])
                     .OfType<UriCitationMessageAnnotation>())
        {
            if (citation.Uri is not { IsAbsoluteUri: true } uri || uri.Scheme is not ("http" or "https"))
                continue;

            if (!seen.Add(uri.AbsoluteUri))
                continue;

            yield return (uri.AbsoluteUri, string.IsNullOrWhiteSpace(citation.Title) ? uri.Host : citation.Title);
        }
    }
}
