using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class TelegramBotClientExtensions
{
    public static async Task TrySendTextMessageAsync(this ITelegramBotClient client,
        ChatId chatId,
        string text,
        ParseMode parseMode = ParseMode.Html,
        bool disableWebPagePreview = false,
        bool disableNotification = false,
        int replyToMessageId = 0,
        IReplyMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                parseMode: parseMode,
                disableWebPagePreview: disableWebPagePreview,
                disableNotification: disableNotification,
                replyToMessageId: replyToMessageId,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch  (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
        }
    }
    
    public static async Task TrySendStickerAsync(
        this ITelegramBotClient client,
        ChatId chatId,
        InputFile sticker,
        bool disableNotification = false,
        int replyToMessageId = 0,
        IReplyMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SendStickerAsync(
                chatId:chatId, 
                sticker:sticker, 
                disableNotification:disableNotification, 
                protectContent:null, 
                replyToMessageId:replyToMessageId,
                allowSendingWithoutReply:null, 
                replyMarkup:replyMarkup, 
                cancellationToken:cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
        }
    }
}

