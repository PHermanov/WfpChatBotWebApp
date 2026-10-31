namespace WfpChatBotWebApp.Persistence.Entities;

public record Chat
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public bool GameEnabled { get; set; }
    public bool AutoReplyEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public string? Comment { get; set; }
}
