using MediatR;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public static class CommandParser
{
    public static IRequest? Parse(Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            return null;
        }

        var split = message.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var commandName = split[0].ToLower();

        return commandName switch
        {
            "/helpaz" => new HelpCommand(message),
            "/ping" => new PingCommand(message),
            "/echo" => new EchoCommand(message),
            _ => null
        };
    }
}
