using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class TelegramBotClientExtensions
{
    public static async Task<Message?> TrySendTextMessageAsync(this ITelegramBotClient client,
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
            return await client.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                parseMode: parseMode,
                disableWebPagePreview: disableWebPagePreview,
                disableNotification: disableNotification,
                replyToMessageId: replyToMessageId,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
            return default;
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
                chatId: chatId,
                sticker: sticker,
                disableNotification: disableNotification,
                protectContent: null,
                replyToMessageId: replyToMessageId,
                allowSendingWithoutReply: null,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
        }
    }

    public static async Task TrySendPhotoAsync(
        this ITelegramBotClient client,
        ILogger logger,
        ChatId chatId,
        InputFile photo,
        string? caption = null,
        ParseMode parseMode = ParseMode.Html,
        bool disableNotification = false,
        int replyToMessageId = 0,
        IReplyMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await client.SendPhotoAsync(
                chatId: chatId,
                photo: photo,
                caption: caption,
                parseMode: parseMode,
                disableNotification: disableNotification,
                replyToMessageId: replyToMessageId,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(nameof(TrySendPhotoAsync) + ":" + exception.GetType());
            logger.LogError(exception.Message);
        }
    }

    public static async Task TryEditMessageTextAsync(this ITelegramBotClient client,
        ChatId chatId,
        int messageId,
        string text,
        ParseMode parseMode = ParseMode.MarkdownV2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: text,
                parseMode: parseMode,
                entities: null,
                disableWebPagePreview: null,
                replyMarkup: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
        }
    }
}