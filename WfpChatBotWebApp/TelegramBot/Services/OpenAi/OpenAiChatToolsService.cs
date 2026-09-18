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
        CancellationToken cancellationToken,
        ImageToolContext? imageContext = null);
}

public class OpenAiChatToolsService(
    IAiImageService aiImageService,
    IAiImageEditService imageEditService,
    ILogger<OpenAiChatToolsService>? logger = null)
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
                            "description": "The visual description of a new image to generate. Use EditImage instead when modifying a supplied image."
                        }
                    },
                    "required": [ "prompt" ],
                    "additionalProperties": false
                }
                """),
            strictModeEnabled: true);

        var editImageTool = ResponseTool.CreateFunctionTool(
            functionName: nameof(imageEditService.EditImage),
            functionDescription: "Edits the current attached image or the image being replied to. Requires a supplied source image; preserves the subject while applying the requested changes. Source bytes are supplied by the application, not tool arguments.",
            functionParameters: BinaryData.FromString(
                """
                {
                    "type": "object",
                    "properties": {
                        "prompt": {
                            "type": "string",
                            "description": "Describe the changes to the supplied image and what should be preserved."
                        }
                    },
                    "required": [ "prompt" ],
                    "additionalProperties": false
                }
                """),
            strictModeEnabled: true);

        return [createImageTool, editImageTool];
    }

    public async IAsyncEnumerable<OpenAiResponse> GetToolCallOutput(
        FunctionCallResponseItem toolCall,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ImageToolContext? imageContext = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (toolCall.FunctionName != nameof(aiImageService.CreateImage) && toolCall.FunctionName != nameof(imageEditService.EditImage))
            throw new InvalidOperationException($"Unknown OpenAI tool '{toolCall.FunctionName}'.");

        using var argumentsDocument = JsonDocument.Parse(toolCall.FunctionArguments);
        if (argumentsDocument.RootElement.ValueKind != JsonValueKind.Object ||
            !argumentsDocument.RootElement.TryGetProperty("prompt", out var promptElement) ||
            promptElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(promptElement.GetString()))
        {
            throw new InvalidOperationException($"The {toolCall.FunctionName} tool requires a non-empty string prompt.");
        }

        var prompt = promptElement.GetString()!;
        if (toolCall.FunctionName == nameof(imageEditService.EditImage))
        {
            yield return await Edit(prompt, imageContext, cancellationToken);
            yield break;
        }
        await foreach (var imageBytes in aiImageService.CreateImage(prompt, cancellationToken: cancellationToken))
        {
            if (imageBytes.Length == 0)
                throw new InvalidOperationException("CreateImage returned an empty image.");
            yield return new OpenAiResponse
            {
                ContentType = OpenAiContentType.ImageBytes,
                ImageContent = imageBytes,
                ContentComplete = true
            };
        }
    }

    private async Task<OpenAiResponse> Edit(string prompt, ImageToolContext? context, CancellationToken cancellationToken)
    {
        if (context?.SourceImage is null)
            return TextResult(context?.MissingImageMessage, "EditImage requires a current or replied image.");
        try
        {
            await foreach (var bytes in imageEditService.EditImage(prompt, context.SourceImage, cancellationToken: cancellationToken))
            {
                if (bytes.Length == 0) break;
                return new OpenAiResponse { ContentType = OpenAiContentType.ImageBytes, ImageContent = bytes, ContentComplete = true };
            }
            throw new InvalidOperationException("EditImage returned no image bytes.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger?.LogWarning("EditImage tool failed: {ErrorType}", e.GetType().Name);
            return TextResult(context.EditFailureMessage, "EditImage failed to produce an image.");
        }
    }

    private static OpenAiResponse TextResult(string? text, string error)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException(error);
        return new OpenAiResponse { ContentType = OpenAiContentType.Text, Content = text, ContentComplete = true };
    }
}
