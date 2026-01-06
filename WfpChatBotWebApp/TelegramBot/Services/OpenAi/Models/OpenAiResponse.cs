namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

public class OpenAiResponse
{
    public OpenAiContentType ContentType { get; init; } = OpenAiContentType.Text;
    public string Content { get; init; } = string.Empty;
    public bool ContentComplete { get; init; }
}

public enum OpenAiContentType
{
    Text = 1,
    ImageUrl = 2
}
