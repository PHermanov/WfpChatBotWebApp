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
    IContextKeysService contextKeysService,
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
            && contextKeysService.ContainsKey(GetKey(message.Chat.Id, message.ReplyToMessage.MessageId));

        var contextKey = GetContextKey(message);

        try
        {
            var requests = await CreateRequestsQueue(message, repliedMessageInContext, cancellationToken);
            var imageContext = await CreateImageToolContext(message, requests, cancellationToken);
            var previousContentLength = 0;

            await foreach (var response in openAiChatService.ProcessMessage(contextKey.Value, message.Chat.Id, requests, cancellationToken, imageContext))
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

                answerMessage = await EditMessage(
                    answerMessage,
                    response,
                    cancellationToken);

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
            SetContextKey(answerMessage, contextKey);
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

    private async Task<ImageToolContext> CreateImageToolContext(Message message, OpenAiRequest[] requests, CancellationToken cancellationToken)
    {
        var source = requests[^1].Image;
        if (source is null && message.ReplyToMessage is not null)
            source = requests.Length > 1 ? requests[0].Image : (await CreateRequest(message.ReplyToMessage, cancellationToken)).Image;

        var missing = await messageService.GetMessageByNameAsync(Messages.ImageSourceMissing, cancellationToken);
        if (string.IsNullOrWhiteSpace(missing))
            missing = await messageService.GetMessageByNameAsync(Messages.WhatWanted, cancellationToken);
        var failed = await messageService.GetMessageByNameAsync(Messages.ImageEditFailed, cancellationToken);
        if (string.IsNullOrWhiteSpace(failed))
            failed = await messageService.GetMessageByNameAsync(Messages.FuckOff, cancellationToken);
        return new ImageToolContext(source, missing, failed);
    }

    private async Task<OpenAiRequest> CreateRequest(
        Message message,
        CancellationToken cancellationToken) =>
        new()
        {
            UserId = message.From?.Id,
            MessageText = message.GetMessageText(),
            Image = await botClient.GetPhotoFromMessage(message, cancellationToken) ?? await botClient.GetStickerFromMessage(message, cancellationToken)
        };

    private async Task<Message> EditMessage(
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

        return updatedMessage ?? message;

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

    private static string GetKey(long chatId, int messageId) => $"{chatId}_{messageId}";

    private KeyValuePair<string, Guid> GetContextKey(Message message)
    {
        var key = message.ReplyToMessage is not null
            ? GetKey(message.Chat.Id, message.ReplyToMessage.MessageId)
            : GetKey(message.Chat.Id, message.MessageId);

        if (contextKeysService.TryGetValue(key, out var contextKey))
        {
            return new KeyValuePair<string, Guid>(key, contextKey);
        }
        else
        {
            var newContextKey = Guid.NewGuid();
            contextKeysService.SetValue(key, newContextKey);
            return new KeyValuePair<string, Guid>(key, newContextKey);
        }
    }

    private void SetContextKey(Message answer, KeyValuePair<string, Guid> prevKey)
    {
        var key = GetKey(answer.Chat.Id, answer.MessageId);
        contextKeysService.SetValue(key, prevKey.Value);
        contextKeysService.RemoveValue(prevKey.Key);
    }
}