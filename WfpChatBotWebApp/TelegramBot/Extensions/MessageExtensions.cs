using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class MessageExtensions
{
    public static string? GetMessageText(this Message message)
        => message.Type switch
        {
            MessageType.Text => message.Text,
            MessageType.Photo => message.Caption,
            _ => string.Empty
        };
}