using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Extensions;

public static class OpenAiChatToolsServiceExtensions
{
    extension(IOpenAiChatToolsService openAiChatToolsService)
    {
        public CreateResponseOptions RegisterTools(CreateResponseOptions responseOptions)
        {
            responseOptions.ToolChoice = ResponseToolChoice.CreateAutoChoice();

            foreach (var tool in openAiChatToolsService.GetRegisteredTools())
            {
                responseOptions.Tools.Add(tool);
            }

            return responseOptions;
        }
    }
}
