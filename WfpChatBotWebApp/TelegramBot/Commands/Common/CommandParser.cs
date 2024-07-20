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
            "/help" => new HelpCommand(message),
            "/ping" => new PingCommand(message),
            "/echo" => new EchoCommand(message),
            "/g" => new GoogleCommand(message),
            "/me" => new MeCommand(message),
            "/today" => new TodayCommand(message),
            "/yesterday" => new YesterdayCommand(message),
            "/tomorrow" => new TomorrowCommand(message),
            "/month" => new MonthCommand(message),
            "/all" => new AllCommand(message),
            "/mamota" => new MamotaCommand(message),
            "/year" => new YearCommand(message),
            "/draw" => new DrawCommand(message),
            _ => null
        };
    }
}
