using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI.Responses;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

#pragma warning disable OPENAI001

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class WebCitationFormatterTests
{
    [Fact]
    public void AppendSources_LeavesTextUnchanged_WhenNoAnnotations()
    {
        var message = ResponseItem.CreateAssistantMessageItem("Hello");
        var formatted = WebCitationFormatter.AppendSources("Hello", [message]);

        Assert.Equal("Hello", formatted);
    }

    [Fact]
    public void AppendSources_EncodesDeduplicatesAndFallsBackToHost()
    {
        var message = MessageWithCitations(
            ("https://example.com/a?x=1&y=2", "Title <b>&</b> \"quoted\""),
            ("https://example.com/a?x=1&y=2", "Duplicate"),
            ("https://news.example.org/story", ""),
            ("ftp://files.example.com/file", "Not http"));

        var formatted = WebCitationFormatter.AppendSources("Answer", [message]);

        Assert.Equal(
            "Answer\n\n" +
            "🔗 <a href=\"https://example.com/a?x=1&amp;y=2\">Title &lt;b&gt;&amp;&lt;/b&gt; &quot;quoted&quot;</a>\n" +
            "🔗 <a href=\"https://news.example.org/story\">news.example.org</a>",
            formatted);
    }

    [Fact]
    public void AppendSources_KeepsAtMostFiveSources()
    {
        var message = MessageWithCitations(Enumerable.Range(1, 7)
            .Select(i => ($"https://example.com/{i}", $"Source {i}"))
            .ToArray());

        var formatted = WebCitationFormatter.AppendSources("Answer", [message]);

        Assert.Equal(5, formatted.Split('\n').Count(line => line.StartsWith("🔗 ", StringComparison.Ordinal)));
        Assert.Contains("Source 5", formatted);
        Assert.DoesNotContain("Source 6", formatted);
    }

    [Fact]
    public void AppendSources_LeavesEmptyTextUnchanged()
    {
        var message = MessageWithCitations(("https://example.com", "Example"));

        Assert.Equal(string.Empty, WebCitationFormatter.AppendSources(string.Empty, [message]));
    }

    private static MessageResponseItem MessageWithCitations(params (string Url, string Title)[] citations)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "message", id = "msg_test", role = "assistant", status = "completed",
            content = new[]
            {
                new
                {
                    type = "output_text", text = "Answer",
                    annotations = citations.Select(citation => new
                    {
                        type = "url_citation", url = citation.Url, title = citation.Title, start_index = 0, end_index = 6
                    }).ToArray()
                }
            }
        });

        return Assert.IsType<MessageResponseItem>(ModelReaderWriter.Read<ResponseItem>(BinaryData.FromString(json)), exactMatch: false);
    }
}
