using MediatR;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public static class CommandParser
{
    public static bool TryParse(Message message, out IRequest command)
    {
        command = null!;

        var split = message.Text?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        if (!split.Any())
        {
            return false;
        }

        var commandName = split[0].ToLower();

        switch (commandName)
        {
            case "/helpaz":
                {
                    command = new HelpCommand { ChatId = message.Chat.Id };
                    return true;
                }
            case "ping":
                {
                    command = new PingCommand { ChatId = message.Chat.Id };
                    return true;
                }
        }

        return false;
    }
}
