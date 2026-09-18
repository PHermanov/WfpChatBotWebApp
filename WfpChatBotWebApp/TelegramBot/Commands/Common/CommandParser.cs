using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public static class CommandParser
{
    public static CommandBase? Parse(Message message, string? botUserName = null)
    {
        var split = CommandText.Split(message);
        if (split.Length == 0)
            return null;
        var commandName = split[0].ToLowerInvariant();
        var suffix = commandName.IndexOf('@');
        if (suffix >= 0)
        {
            if (!string.Equals(commandName[(suffix + 1)..], botUserName, StringComparison.OrdinalIgnoreCase))
                return null;
            commandName = commandName[..suffix];
        }

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
            "/redraw" => new RedrawCommand(message),
            _ => null
        };
    }
}
