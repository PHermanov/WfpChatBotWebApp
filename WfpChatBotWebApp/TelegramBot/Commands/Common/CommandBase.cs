using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandBase(Message message)
{
    public virtual string Name => string.Empty;
    public long ChatId { get; } = message.Chat.Id;
    public string FromMention => $"[{(FromName.StartsWith("@") ? FromName : $"@{FromName}")}](tg://user?id={FromId})";
    public int MessageId { get; } = message.MessageId;
    public Message Message { get; } = message;
    private long FromId { get; } = message.From?.Id ?? -1;
    private string FromName { get; } = message.From?.Username ?? string.Empty;
}