namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class OpenAiRequest
{
    public long? UserId { get; set; }
    public string? MessageText { get; set; }
    public BinaryData? Image { get; set; }
}