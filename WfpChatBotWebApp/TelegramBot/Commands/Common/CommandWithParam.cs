using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandWithParam(Message message) : CommandBase(message)
{
    public string Param { get; } = CommandText.Split(message) is [_, var param] ? param : string.Empty;
}