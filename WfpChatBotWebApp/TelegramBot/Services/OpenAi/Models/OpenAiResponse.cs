namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

public class OpenAiResponse
{
    public OpenAiContentType ContentType { get; set; } = OpenAiContentType.Text;
    public string Content { get; set; } = string.Empty;
}

public enum OpenAiContentType
{
    Text = 1,
    ImageUrl = 2
}
