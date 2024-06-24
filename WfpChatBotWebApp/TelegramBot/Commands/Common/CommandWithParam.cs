using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandWithParam(Message message) : CommandBase(message)
{
    public string Param { get; } = message.Text!.TrimEnd().Contains(' ') 
        ? message.Text[message.Text.IndexOf(' ')..] 
        : string.Empty;
}