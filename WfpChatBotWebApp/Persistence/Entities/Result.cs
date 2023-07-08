namespace WfpChatBotWebApp.Persistence.Entities;

public class Result
{
    public int Id { get; set; }
    public int ChatId { get; set; }
    public int UserId { get; set; }
    public DateTime PlayedAt { get; set; }
}
