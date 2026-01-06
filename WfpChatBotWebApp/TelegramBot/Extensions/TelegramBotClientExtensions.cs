using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class TelegramBotClientExtensions
{
    public static async Task<Message?> TrySendTextMessageAsync(this ITelegramBotClient client,
        ChatId chatId,
        string text,
        ILogger logger,
        ParseMode parseMode = ParseMode.Html,
        bool disableNotification = false,
        int replyToMessageId = 0,
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
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception in {Name}", nameof(TrySendTextMessageAsync));
            return null;
        }
    }

    public static async Task TrySendStickerAsync(
        this ITelegramBotClient client,
        ChatId chatId,
        InputFile sticker,
        ILogger logger,
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
           logger.LogError(exception, "Exception in {Name}", nameof(TrySendStickerAsync));
        }
    }

    public static async Task<Message?> TrySendPhotoAsync(
        this ITelegramBotClient client,
        ILogger logger,
        ChatId chatId,
        InputFile photo,
        string? caption = null,
        ParseMode parseMode = ParseMode.Html,
        bool disableNotification = false,
        int replyToMessageId = 0,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await client.SendPhoto(
                chatId: chatId,
                photo: photo,
                caption: caption,
                parseMode: parseMode,
                disableNotification: disableNotification,
                replyParameters: replyToMessageId,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception in {Name}", nameof(TrySendPhotoAsync));

            return null;
        }
    }

    public static Task<Message?> TryEditMessageTextAsync(this ITelegramBotClient client,
        Message message,
        string text,
        ILogger logger,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default) =>
        client.TryEditMessageTextAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId,
            text: text,
            logger: logger,
            parseMode: parseMode,
            cancellationToken: cancellationToken);

    public static async Task<Message?> TryEditMessageTextAsync(this ITelegramBotClient client,
        ChatId chatId,
        int messageId,
        string text,
        ILogger logger,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.EditMessageText(
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
            logger.LogError(exception, "Exception in {Name}", nameof(TryEditMessageTextAsync));

            return null;
        }
    }

    public static Task<Message?> TryEditMessageCaptionAsync(this ITelegramBotClient client,
        Message message,
        string? caption,
        ILogger logger,
        bool showCaptionAboveMedia = false,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default) =>
        client.TryEditMessageCaptionAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId,
            caption: caption,
            logger: logger,
            showCaptionAboveMedia: showCaptionAboveMedia,
            parseMode: parseMode,
            cancellationToken: cancellationToken);

    public static async Task<Message?> TryEditMessageCaptionAsync(this ITelegramBotClient client,
        ChatId chatId,
        int messageId,
        string? caption,
        ILogger logger,
        bool showCaptionAboveMedia = false,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.EditMessageCaption(
                chatId: chatId,
                messageId: messageId,
                caption: caption,
                replyMarkup: null,
                parseMode: parseMode,
                captionEntities: null,
                showCaptionAboveMedia: showCaptionAboveMedia,
                businessConnectionId: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception in {Name}", nameof(TryEditMessageCaptionAsync));

            return null;
        }
    }
    
    public static Task<Message?> TryEditMessageMediaAsync(this ITelegramBotClient client,
        Message message,
        InputMedia media,
        ILogger logger,
        CancellationToken cancellationToken = default) =>
        client.TryEditMessageMediaAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId,
            media: media,
            logger: logger,
            cancellationToken: cancellationToken);

    public static async Task<Message?> TryEditMessageMediaAsync(this ITelegramBotClient client,
        ChatId chatId,
        int messageId,
        InputMedia media,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.EditMessageMedia(
                chatId: chatId,
                messageId: messageId,
                media: media,
                replyMarkup: null,
                businessConnectionId: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception in {Name}", nameof(TryEditMessageMediaAsync));

            return null;
        }
    }

    public static async Task TrySendVideoAsync(
        this ITelegramBotClient client,
        ChatId chatId,
        InputFile video,
        ILogger logger,
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
           logger.LogError(exception, "Exception in {Name}", nameof(TrySendVideoAsync));
        }
    }
    
    public static async ValueTask<BinaryData?> GetPhotoFromMessage(
        this ITelegramBotClient client,
        Message message,
        CancellationToken cancellationToken)
    {
        var fileId = message.Photo?[^1].FileId;
        
        if (fileId != null)
        {
            var imageFile = await client.GetFile(fileId, cancellationToken);
            
            if (!string.IsNullOrEmpty(imageFile.FilePath))
            {
                using var imageStream = new MemoryStream();
                await client.DownloadFile(imageFile.FilePath, imageStream, cancellationToken);
                return new BinaryData(imageStream.ToArray());
            }
        }

        return null;
    }
}