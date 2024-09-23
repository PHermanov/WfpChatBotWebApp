using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandBase(Message message)
{
    public virtual string Name { get; } = string.Empty;
    public long ChatId { get; } = message.Chat.Id;
    public string FromMention => @$"[{(FromName.StartsWith("@") ? FromName : $"@{FromName}")}](tg://user?id={FromId})";
    public int MessageId { get; } = message.MessageId;
    private long FromId { get; } = message.From?.Id ?? -1;
    private string FromName { get; } = message.From?.Username ?? string.Empty;
}