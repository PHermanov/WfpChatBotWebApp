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
        bool disableNotification = false,
        int replyToMessageId = 0,
        IReplyMarkup? replyMarkup = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: parseMode,
                disableNotification: disableNotification,
                replyParameters: replyToMessageId,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Exception in {Name}", nameof(TrySendTextMessageAsync));
            return null;
        }
    }

    public static async Task TrySendStickerAsync(
        this ITelegramBotClient client,
        ChatId chatId,
        InputFile sticker,
        int replyToMessageId = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SendSticker(
                chatId: chatId,
                sticker: sticker,
                replyParameters: replyToMessageId,
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
            await client.SendPhoto(
                chatId: chatId,
                photo: photo,
                caption: caption,
                parseMode: parseMode,
                disableNotification: disableNotification,
                replyParameters: replyToMessageId,
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
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: text,
                parseMode: parseMode,
                entities: null,
                replyMarkup: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
        }
    }

    public static async Task TrySendVideoAsync(
        this ITelegramBotClient client,
        ChatId chatId,
        InputFile video,
        int? replyToMessageId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SendVideo(
                chatId: chatId,
                video: video,
                replyParameters: replyToMessageId,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.GetType());
            Console.WriteLine(exception.Message);
        }
    }
}