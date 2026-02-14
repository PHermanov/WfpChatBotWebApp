using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot.Extensions;

public static class TelegramBotClientExtensions
{
    extension(ITelegramBotClient client)
    {
        public async Task<Message?> TrySendTextMessageAsync(
            ChatId chatId,
            string text,
            ILogger logger,
            ParseMode parseMode,
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

        public async Task TrySendStickerAsync(
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

        public async Task<Message?> TrySendPhotoAsync(
            ILogger logger,
            ChatId chatId,
            InputFile photo,
            ParseMode parseMode,
            string? caption = null,
            bool disableNotification = false,
            int replyToMessageId = 0,
            CancellationToken cancellationToken = default)
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

        public async Task<Message?> TryEditMessageTextAsync(
            Message message,
            string text,
            ILogger logger,
            ParseMode parseMode,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (message.Text == text)
                    return message;

                return await client.EditMessageText(
                    chatId: message.Chat.Id,
                    messageId: message.Id,
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

        public async Task<Message?> TryEditMessageCaptionAsync(
            Message message,
            string? caption,
            ILogger logger,
            ParseMode parseMode,
            bool showCaptionAboveMedia = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.EditMessageCaption(
                    chatId: message.Chat.Id,
                    messageId: message.Id,
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

        public async Task<Message?> TryEditMessageMediaAsync(
            Message message,
            InputMedia media,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await client.EditMessageMedia(
                    chatId: message.Chat.Id,
                    messageId: message.Id,
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

        public async Task TrySendVideoAsync(
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

        public async ValueTask<BinaryData?> GetPhotoFromMessage(
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

        public async ValueTask<BinaryData?> GetStickerFromMessage(
            Message message,
            CancellationToken cancellationToken)
        {
            if (message.Sticker?.IsAnimated == true)
                return null;

            var fileId = message.Sticker?.FileId;

            if (fileId != null)
            {
                var stickerFile = await client.GetFile(fileId, cancellationToken);
                if (!string.IsNullOrEmpty(stickerFile.FilePath))
                {
                    using var stickerStream = new MemoryStream();
                    await client.DownloadFile(stickerFile.FilePath, stickerStream, cancellationToken);
                    return new BinaryData(stickerStream.ToArray());
                }
            }
            return null;
        }
    }
}