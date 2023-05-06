using MediatR;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public static class CommandParser
{
    public static bool TryParse(Message message, out IRequest command)
    {
        command = null!;

        if (string.IsNullOrEmpty(message.Text))
        {
            return false;
        }

        var split = message.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var commandName = split[0].ToLower();

        switch (commandName)
        {
            case "/helpaz":
                {
                    command = new HelpCommand { ChatId = message.Chat.Id };
                    return true;
                }
            case "/ping":
                {
                    command = new PingCommand { ChatId = message.Chat.Id };
                    return true;
                }
            case "/echo":
                {
                    command = new EchoCommand
                    {
                        ChatId = message.Chat.Id, 
                        Text = split.Length > 1 ? message.Text[message.Text.IndexOf(' ')..] : "Default echo answer" 

                    };
                    return true;
                }
        }

        return false;
    }
}
