namespace WfpChatBotWebApp.Persistence.Entities;

public record ReplyMessage
{
    public string MessageKey { get; set; } = string.Empty;
    public string MessageValue { get; set; } = string.Empty;
}