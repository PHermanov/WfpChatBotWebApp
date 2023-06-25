using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandWithParam : CommandBase
{
    public string Param { get; }

    protected CommandWithParam(Message message) : base(message)
    {
        Param = message.Text!.TrimEnd().Contains(' ') ? message.Text[message.Text.IndexOf(' ')..] : string.Empty;
    }
}