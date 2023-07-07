namespace WfpChatBotWebApp.Persistence.Entities;

public class User
{
    public int ChatId { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public bool Inactive { get; set; }
}
