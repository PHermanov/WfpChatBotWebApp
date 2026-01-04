namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

public class OpenAiRequest
{
    public long? UserId { get; set; }
    public string? MessageText { get; set; }
    public BinaryData? Image { get; set; }
}