namespace WfpChatBotWebApp.Persistence.Entities;

public record User
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public bool Inactive { get; set; }
}
