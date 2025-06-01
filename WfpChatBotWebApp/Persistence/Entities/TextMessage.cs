namespace WfpChatBotWebApp.Persistence.Entities;

public record TextMessage
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}