using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public abstract class CommandBase
{
    public long ChatId { get; }

    protected CommandBase(Message message)
     => ChatId = message.Chat.Id;
}