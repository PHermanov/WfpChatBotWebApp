namespace WfpChatBotWebApp.Persistence.Entities;

public class Result
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public DateTime PlayedAt { get; set; }
}
