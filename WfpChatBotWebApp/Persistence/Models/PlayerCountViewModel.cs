namespace WfpChatBotWebApp.Persistence.Models;

public class PlayerCountViewModel
{
    public long UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public int Count { get; set; }
    public DateTime LastWin { get; init; }

    public override string ToString()
        => $"<i>{UserName}</i>: <b>{Count}</b>";
}