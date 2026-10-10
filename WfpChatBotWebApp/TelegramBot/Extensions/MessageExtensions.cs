using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class MessageExtensions
{
    extension(Message message)
    {
        public string? GetMessageText()
            => message.Type switch
            {
                MessageType.Text => message.Text,
                MessageType.Photo => message.Caption,
                _ => string.Empty
            };

        public string? GetImageFileId()
            => message.Photo?.MaxBy(p => (long)p.Width * p.Height)?.FileId
               ?? (message.Sticker is { IsAnimated: false, IsVideo: false } sticker ? sticker.FileId : null);
    }
}