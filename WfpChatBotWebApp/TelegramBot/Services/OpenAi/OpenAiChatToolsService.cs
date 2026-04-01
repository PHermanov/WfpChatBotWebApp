using HtmlAgilityPack;
using NReadability;
using OpenAI.Chat;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using WfpChatBotWebApp.TelegramBot.Services.InternetFetch;
using WfpChatBotWebApp.TelegramBot.Services.InternetSearch;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiChatToolsService
{
    ChatTool[] GetRegisteredTools();

    IAsyncEnumerable<OpenAiResponse> GetToolCallOutput(
        ChatToolCall toolCall,
        CancellationToken cancellationToken);
}

public class OpenAiChatToolsService(
    IAiImageService aiImageService,
    IInternetSearchService internetSearchService,
    IPageFetcher pageFetcher)
    : IOpenAiChatToolsService
{
    public ChatTool[] GetRegisteredTools()
    {
        var createImageTool = ChatTool.CreateFunctionTool(
            functionName: nameof(aiImageService.CreateImage),
            functionDescription: "Creates an image by provided prompt.",
            functionParameters: BinaryData.FromString(
                """
                {
                    "type": "object",
                    "properties": {
                        "prompt": {
                            "type": "string",
                            "description": "The prompt to generate an image. The prompt should be written according to DALL-E 3 content policy."
                        }
                    },
                    "required": [ "prompt" ]
                }
                """));

        var internetSearchTool = ChatTool.CreateFunctionTool(
            functionName: "web_search",
            functionDescription: "Search the internet for up-to-date information",
            functionParameters: BinaryData.FromString(
                """
                {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The search query to find relevant information on the internet."
                        }
                    },
                    "required": [ "query" ]
                }
                """));

        return [createImageTool, internetSearchTool];
    }

    public async IAsyncEnumerable<OpenAiResponse> GetToolCallOutput(
        ChatToolCall toolCall,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (toolCall.FunctionName == nameof(aiImageService.CreateImage))
        {
            using var argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);

            if (argumentsDocument.RootElement.TryGetProperty("prompt", out var promptElement))
            {
                var prompt = promptElement.GetString() ?? string.Empty;

                await foreach (var (imageUrl, imageBytes) in aiImageService.CreateImage(prompt, cancellationToken: cancellationToken))
                {
                    if (imageUrl != null)
                    {
                        yield return new OpenAiResponse
                        {
                            ContentType = OpenAiContentType.ImageUrl,
                            Content = imageUrl,
                            ContentComplete = true
                        };
                    }
                    else if (imageBytes != null)
                    {
                        yield return new OpenAiResponse
                        {
                            ContentType = OpenAiContentType.ImageBytes,
                            ImageContent = imageBytes,
                            ContentComplete = true
                        };
                    }
                }
            }
        }
        else if (toolCall.FunctionName == "web_search")
        {
            using var argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);

            if (argumentsDocument.RootElement.TryGetProperty("query", out var queryElement))
            {
                var query = queryElement.GetString() ?? string.Empty;

                var searchResults = await internetSearchService.Search(query, 3, cancellationToken);

                if (searchResults == null || searchResults.Length == 0)
                {
                    yield return new OpenAiResponse
                    {
                        ContentType = OpenAiContentType.Text,
                        Content = "Not found any relevant information in web"
                    };
                    yield break;
                }

                var tasks = searchResults.Select(u => pageFetcher.Fetch(u.Link, cancellationToken));
                var pagesContent = await Task.WhenAll(tasks);

                // TODO: validate, filter, etc
                foreach (var result in pagesContent)
                {
                    var (title, content) = ExtractArticle(result);
                    yield return new OpenAiResponse
                    {
                        ContentType = OpenAiContentType.Text,
                        Content = $"Content from WEB: \n Title: {title} \n Contnent: {content}"
                    };
                }
            }
        }
    }

    private static (string title, string contnet) ExtractArticle(string html)
    {
        html = Regex.Replace(
                html,
                @"data:image\/[a-zA-Z]+;base64,[^""]+",
                "",
                RegexOptions.IgnoreCase);

        (string title, string content) result = (string.Empty, string.Empty);

        var transcoder = new NReadabilityWebTranscoder();

        try
        {
            var transcodingResult = transcoder.Transcode(new WebTranscodingInput(html));

            if (transcodingResult.TitleExtracted)
                result.title = transcodingResult.ExtractedTitle;

            if (transcodingResult.ContentExtracted)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(transcodingResult.ExtractedContent);

                result.content = doc.DocumentNode.InnerText;
            }
           
        }
        catch
        {
            result.content = ExtractSimple(html);
        }

        return result;
    }

    private static string ExtractSimple(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodes = doc.DocumentNode.SelectNodes("//p");

        if (nodes == null)
            return "";

        return string.Join("\n\n",
            nodes.Select(n => n.InnerText)
                 .Where(t => t.Length > 40));
    }
}
