namespace WfpChatBotWebApp.Persistence.Entities;

public class Result
{
    public int ChatId { get; set; }
    public int UserId { get; set; }
    public DateOnly PlayedAt { get; set; }
}
