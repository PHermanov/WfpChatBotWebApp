using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandBase
{
    public long ChatId { get; }
    public string FromMention => @$"[{(FromName.StartsWith("@") ? FromName : $"@{FromName}")}](tg://user?id={FromId})";
    public int MessageId { get; }
    private long FromId { get; }
    private string FromName { get; }

    protected CommandBase(Message message)
    {
        ChatId = message.Chat.Id;
        FromId = message.From?.Id ?? -1;
        MessageId = message.MessageId;
        FromName = message.From?.Username ?? string.Empty;
    }
}