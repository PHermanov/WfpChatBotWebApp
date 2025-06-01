namespace WfpChatBotWebApp.Persistence.Entities;

public record Result
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public DateTime PlayDate { get; set; }
}
