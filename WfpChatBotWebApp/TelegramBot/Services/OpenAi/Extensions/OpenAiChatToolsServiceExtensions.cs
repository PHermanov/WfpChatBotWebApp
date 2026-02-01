using OpenAI.Chat;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Extensions;

public static class OpenAiChatToolsServiceExtensions
{
    extension(IOpenAiChatToolsService openAiChatToolsService)
    {
        public ChatCompletionOptions RegisterTools(ChatCompletionOptions chatCompletionOptions)
        {
            chatCompletionOptions.ToolChoice = ChatToolChoice.CreateAutoChoice();

            foreach (var tool in openAiChatToolsService.GetRegisteredTools())
            {
                chatCompletionOptions.Tools.Add(tool);
            }

            return chatCompletionOptions;
        }
    }
}
