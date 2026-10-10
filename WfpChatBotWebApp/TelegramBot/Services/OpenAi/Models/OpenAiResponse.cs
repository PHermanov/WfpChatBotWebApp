namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

public class OpenAiResponse
{
    public OpenAiContentType ContentType { get; init; } = OpenAiContentType.Text;
    public string Content { get; init; } = string.Empty;
    public byte[]? ImageContent { get; init; } = null;
    public bool ContentComplete { get; init; }

    // Set by the consumer after an image is shown in Telegram, before it resumes enumeration; the producer reads it after the yield.
    public int? DeliveredMessageId { get; set; }
}

public enum OpenAiContentType
{
    Text = 1,
    ImageBytes = 3
}
