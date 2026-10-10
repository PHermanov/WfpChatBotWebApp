using System.Xml;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IBotReplyService
{
    Task Reply(Message message, CancellationToken cancellationToken);
}

public class BotReplyService(
    ITelegramBotClient botClient,
    IOpenAiChatService openAiChatService,
    ITextMessageService messageService,
    IConversationStore conversationStore,
    ILogger<BotReplyService> logger)
    : IBotReplyService
{
    const string NonCompleteMessagePostfix = "...";

    public async Task Reply(Message message, CancellationToken cancellationToken)
    {
        var answerMessage = await botClient.TrySendTextMessageAsync(
            chatId: message.Chat.Id,
            text: NonCompleteMessagePostfix,
            parseMode: ParseMode.Html,
            replyToMessageId: message.MessageId,
            logger: logger,
            cancellationToken: cancellationToken);

        if (answerMessage is null)
            return;

        // Capture this before GetContextKey, which registers the replied message key itself.
        var repliedMessageInContext = message.ReplyToMessage is not null
            && conversationStore.ContainsKey(TelegramThreadKey.GetMessageKey(message.Chat.Id, message.ReplyToMessage.MessageId));

        var threadKey = TelegramThreadKey.GetThreadKey(message);

        // The newest image this turn adds to the conversation: the current one, otherwise a replied one sent as a new request.
        var lastImage = GetConversationImage(message)
            ?? (repliedMessageInContext ? null : GetConversationImage(message.ReplyToMessage));

        try
        {
            var requests = await CreateRequestsQueue(message, repliedMessageInContext, cancellationToken);
            var imageContext = await CreateImageToolContext(message, threadKey, requests, cancellationToken);
            var previousContentLength = 0;

            await foreach (var response in openAiChatService.ProcessMessage(threadKey, message.Chat.Id, requests, cancellationToken, imageContext))
            {
                if (response.ContentType is OpenAiContentType.Text && !response.ContentComplete)
                {
                    if (response.Content.Length - previousContentLength < 60)
                        continue;

                    // Validate HTML before updating
                    if (!IsValidHtml(response.Content))
                    {
                        logger.LogDebug("Skipping update due to invalid HTML in incomplete message");
                        continue;
                    }

                    previousContentLength = response.Content.Length;
                }

                var updatedMessage = await EditMessage(
                    answerMessage,
                    response,
                    cancellationToken);

                if (response.ContentType is OpenAiContentType.ImageBytes && GetConversationImage(updatedMessage) is { } deliveredImage)
                {
                    response.DeliveredMessageId = deliveredImage.MessageId;
                    lastImage = deliveredImage;
                }

                answerMessage = updatedMessage ?? answerMessage;

                await Task.Delay(TimeSpan.FromMilliseconds(1100), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger.LogError(e, "BotReplyService Exception");

            var response = new OpenAiResponse
            {
                ContentType = OpenAiContentType.Text,
                Content = await messageService.GetMessageByNameAsync(Messages.FuckOff, cancellationToken),
                ContentComplete = true
            };

            await EditMessage(
                answerMessage,
                response,
                cancellationToken);
        }
        finally
        {
            if (conversationStore.TryGetConversationId(threadKey, out var conversationId))
            {
                conversationStore.SetConversationId(TelegramThreadKey.GetMessageKey(message.Chat.Id, message.MessageId), conversationId);
                conversationStore.SetConversationId(TelegramThreadKey.GetMessageKey(answerMessage.Chat.Id, answerMessage.MessageId), conversationId);
                if (lastImage is not null)
                    conversationStore.SetLastImage(conversationId, lastImage);
            }
        }
    }

    private async Task<OpenAiRequest[]> CreateRequestsQueue(Message message, bool repliedMessageInContext, CancellationToken cancellationToken)
    {
        var requests = new List<OpenAiRequest>();

        // Add the replied message only when it is not already part of the conversation context
        if (message.ReplyToMessage != null && !repliedMessageInContext)
        {
            requests.Add(await CreateRequest(message.ReplyToMessage, cancellationToken));
        }

        requests.Add(await CreateRequest(message, cancellationToken));

        return requests.ToArray();
    }

    private async Task<ImageToolContext> CreateImageToolContext(Message message, string threadKey, OpenAiRequest[] requests, CancellationToken cancellationToken)
    {
        var source = requests[^1].Image;
        int? sourceMessageId = source is null ? null : message.MessageId;
        if (source is null && message.ReplyToMessage is not null)
        {
            source = requests.Length > 1 ? requests[0].Image : (await CreateRequest(message.ReplyToMessage, cancellationToken)).Image;
            if (source is not null)
                sourceMessageId = message.ReplyToMessage.MessageId;
        }

        // Without a current or replied image, EditImage falls back to the last image of the thread's conversation.
        Func<CancellationToken, ValueTask<BinaryData?>>? loadThreadImage = null;
        if (source is null
            && conversationStore.TryGetConversationId(threadKey, out var conversationId)
            && conversationStore.TryGetLastImage(conversationId, out var threadImage))
        {
            loadThreadImage = token => botClient.GetImageByFileId(threadImage.FileId, token);
            sourceMessageId = threadImage.MessageId;
        }

        var missing = await messageService.GetMessageByNameAsync(Messages.ImageSourceMissing, cancellationToken);
        if (string.IsNullOrWhiteSpace(missing))
            missing = await messageService.GetMessageByNameAsync(Messages.WhatWanted, cancellationToken);
        var failed = await messageService.GetMessageByNameAsync(Messages.ImageEditFailed, cancellationToken);
        if (string.IsNullOrWhiteSpace(failed))
            failed = await messageService.GetMessageByNameAsync(Messages.FuckOff, cancellationToken);
        return new ImageToolContext(source, missing, failed, loadThreadImage, sourceMessageId);
    }

    private static ConversationImage? GetConversationImage(Message? message) =>
        message?.GetImageFileId() is { } fileId ? new ConversationImage(message.MessageId, fileId) : null;

    private async Task<OpenAiRequest> CreateRequest(
        Message message,
        CancellationToken cancellationToken) =>
        new()
        {
            UserId = message.From?.Id,
            MessageId = message.MessageId,
            ReplyToMessageId = message.ReplyToMessage?.MessageId,
            MessageText = message.GetMessageText(),
            Image = await botClient.GetPhotoFromMessage(message, cancellationToken) ?? await botClient.GetStickerFromMessage(message, cancellationToken)
        };

    private async Task<Message?> EditMessage(
        Message message,
        OpenAiResponse response,
        CancellationToken cancellationToken)
    {
        Message? updatedMessage;

        switch (response.ContentType)
        {
            case OpenAiContentType.ImageBytes:
                {
                    var caption = message.GetMessageText();

                    using var imageStream = new MemoryStream(response.ImageContent!);
                    var inputFile = InputFile.FromStream(imageStream, "image.png");

                    var inputMediaPhoto = new InputMediaPhoto(inputFile)
                    {
                        ShowCaptionAboveMedia = true,
                        Caption = caption == NonCompleteMessagePostfix
                            ? null
                            : caption
                    };

                    updatedMessage = message.Type switch
                    {
                        MessageType.Photo => await botClient.TrySendPhotoAsync(
                            logger,
                            message.Chat.Id,
                            inputMediaPhoto.Media,
                            replyToMessageId: message.MessageId,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken),
                        _ => await botClient.TryEditMessageMediaAsync(
                            message: message,
                            media: inputMediaPhoto,
                            logger: logger,
                            cancellationToken: cancellationToken)
                    };

                    break;
                }
            default:
                {
                    updatedMessage = message.Type switch
                    {
                        // Need to figure out how to combine several images into a media group to show all generated images in one message
                        MessageType.Photo => await botClient.TryEditMessageCaptionAsync(
                            message: message,
                            parseMode: ParseMode.Html,
                            caption: GetText(response),
                            logger: logger,
                            showCaptionAboveMedia: true,
                            cancellationToken: cancellationToken),
                        _ => await botClient.TryEditMessageTextAsync(
                            message: message,
                            parseMode: ParseMode.Html,
                            text: GetText(response),
                            logger: logger,
                            cancellationToken: cancellationToken)
                    };

                    break;
                }
        }

        return updatedMessage;

        static string GetText(OpenAiResponse response) =>
            response.ContentComplete
                ? response.Content.Replace("<br/>", "\n")
                : $"{response.Content.Replace("<br/>", "\n")} {NonCompleteMessagePostfix}";
    }

    private static bool IsValidHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        try
        {
            var wrappedContent = $"<root>{content}</root>";
            using var reader = new StringReader(wrappedContent);
            using var xmlReader = XmlReader.Create(reader, new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                CheckCharacters = true
            });

            while (xmlReader.Read())
            {
                // Just read through to validate
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

}