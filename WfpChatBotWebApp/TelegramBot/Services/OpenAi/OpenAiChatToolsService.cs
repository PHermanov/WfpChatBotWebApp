using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenAI.Chat;
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
    IOpenAiImageService openAiImageService)
    : IOpenAiChatToolsService
{
    public ChatTool[] GetRegisteredTools()
    {
        var createImageTool = ChatTool.CreateFunctionTool(
            functionName: nameof(openAiImageService.CreateImage),
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

        return [createImageTool];
    }
    
    public async IAsyncEnumerable<OpenAiResponse> GetToolCallOutput(
        ChatToolCall toolCall,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (toolCall.FunctionName == nameof(openAiImageService.CreateImage))
        {
            using var argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);

            if (argumentsDocument.RootElement.TryGetProperty("prompt", out var promptElement))
            {
                var prompt = promptElement.GetString() ?? string.Empty;

                await foreach (var imageUrl in openAiImageService.CreateImage(prompt, cancellationToken: cancellationToken))
                {
                    yield return new OpenAiResponse
                    {
                        ContentType = OpenAiContentType.ImageUrl,
                        Content = imageUrl
                    };
                }
            }
        }
    }
}
