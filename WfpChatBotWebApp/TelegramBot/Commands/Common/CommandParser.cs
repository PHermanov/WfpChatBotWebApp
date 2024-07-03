using MediatR;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public static class CommandParser
{
    private static readonly char[] Separator = [' '];

    public static IRequest? Parse(Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
            return null;

        var split = message.Text.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

        var commandName = split[0].ToLower();

        return commandName switch
        {
            "/helpaz" => new HelpCommand(message),
            "/ping" => new PingCommand(message),
            "/echo" => new EchoCommand(message),
            "/gaz" => new GoogleCommand(message),
            "/meaz" => new MeCommand(message),
            _ => null
        };
    }
}
