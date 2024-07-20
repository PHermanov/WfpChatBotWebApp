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
            "/help_az" => new HelpCommand(message),
            "/ping" => new PingCommand(message),
            "/echo" => new EchoCommand(message),
            "/g_az" => new GoogleCommand(message),
            "/me_az" => new MeCommand(message),
            "/today_az" => new TodayCommand(message),
            "/yesterday_az" => new YesterdayCommand(message),
            "/tomorrow_az" => new TomorrowCommand(message),
            "/month_az" => new MonthCommand(message),
            "/all_az" => new AllCommand(message),
            "/mamota_az" => new MamotaCommand(message),
            "/year_az" => new YearCommand(message),
            "/draw_az" => new DrawCommand(message),
            _ => null
        };
    }
}
