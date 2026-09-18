using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public static class CommandText
{
    private static string GetText(Message message) => message.Text ?? message.Caption ?? string.Empty;

    public static string[] Split(Message message) => GetText(message)
        .Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
