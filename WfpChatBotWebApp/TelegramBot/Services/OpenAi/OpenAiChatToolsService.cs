using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenAI.Responses;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IOpenAiChatToolsService
{
    ResponseTool[] GetRegisteredTools();

    IAsyncEnumerable<OpenAiResponse> GetToolCallOutput(
        FunctionCallResponseItem toolCall,
        CancellationToken cancellationToken);
}

public class OpenAiChatToolsService(
    IAiImageService aiImageService)
    : IOpenAiChatToolsService
{
    public ResponseTool[] GetRegisteredTools()
    {
        var createImageTool = ResponseTool.CreateFunctionTool(
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
                    "required": [ "prompt" ],
                    "additionalProperties": false
                }
                """),
            strictModeEnabled: true);

        return [createImageTool];
    }

    public async IAsyncEnumerable<OpenAiResponse> GetToolCallOutput(
        FunctionCallResponseItem toolCall,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (toolCall.FunctionName != nameof(aiImageService.CreateImage))
            throw new InvalidOperationException($"Unknown OpenAI tool '{toolCall.FunctionName}'.");

        using var argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
        if (argumentsDocument.RootElement.ValueKind != JsonValueKind.Object ||
            !argumentsDocument.RootElement.TryGetProperty("prompt", out var promptElement) ||
            promptElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(promptElement.GetString()))
        {
            throw new InvalidOperationException("The CreateImage tool requires a non-empty string prompt.");
        }

        var prompt = promptElement.GetString()!;
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
