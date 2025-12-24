using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IBotReplyService
{
    Task Reply(string mention, Message message, CancellationToken cancellationToken);
}

public class BotReplyService(
    ITelegramBotClient botClient,
    IOpenAiService openAiService,
    ITextMessageService messageService,
    IContextKeysService contextKeysService,
    ILogger<BotReplyService> logger)
    : IBotReplyService
{
    public async Task Reply(string mention, Message message, CancellationToken cancellationToken)
    {
        var text = GetMessageText(message);
        if (string.IsNullOrWhiteSpace(text))
            return;

        var request = !string.IsNullOrEmpty(mention)
            ? text.Replace($"@{mention}", string.Empty).Trim()
            : text.Trim();

        var answerMessage = await botClient.TrySendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "...",
            parseMode: ParseMode.Markdown,
            replyToMessageId: message.MessageId,
            logger: logger,
            cancellationToken: cancellationToken);

        if (answerMessage == null)
            return;

        var responseBuffer = new StringBuilder();
        var previousBufferLength = 0;

        try
        {
            var imagesBinary = new List<BinaryData>();
            var requests = new List<string> { request };

            var replyToMessage = message.ReplyToMessage;
            if (replyToMessage != null)
            {
                var replyMessageText = GetMessageText(replyToMessage);
                if (string.IsNullOrWhiteSpace(replyMessageText) || !replyMessageText.StartsWith($"@{mention}"))
                {
                    if (!string.IsNullOrWhiteSpace(replyMessageText))
                        requests.Add(replyMessageText);

                    if (replyToMessage.Photo != null)
                    {
                        var photo = await GetPhotoFromMessage(replyToMessage, cancellationToken);
                        if (photo != null)
                            imagesBinary.Add(photo);
                    }
                }
            }

            if (message.Photo != null)
            {
                var photo = await GetPhotoFromMessage(message, cancellationToken);
                if (photo != null)
                    imagesBinary.Add(photo);
            }
            
            var contextKey = GetContextKey(message);
            await foreach (var part in openAiService.ProcessMessage(contextKey.Value, message, requests, imagesBinary, cancellationToken))
            {
                responseBuffer.Append(part);

                if (responseBuffer.Length - previousBufferLength >= 60)
                {
                    previousBufferLength = responseBuffer.Length;

                    await botClient.TryEditMessageTextAsync(
                        chatId: answerMessage.Chat.Id,
                        messageId: answerMessage.MessageId,
                        parseMode: ParseMode.Markdown,
                        text: $"{responseBuffer}...",
                        logger: logger,
                        cancellationToken: cancellationToken);

                    await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                }
            }

            SetContextKey(answerMessage, contextKey);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception at {Class}", nameof(BotReplyService));
            responseBuffer.Clear();
            var offMessage = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.FuckOff, cancellationToken);
            responseBuffer.Append(offMessage);
        }
        finally
        {
            await botClient.TryEditMessageTextAsync(
                chatId: answerMessage.Chat.Id,
                messageId: answerMessage.MessageId,
                text: responseBuffer.ToString(),
                parseMode: ParseMode.Markdown,
                logger: logger,
                cancellationToken: cancellationToken);
        }
    }

    private KeyValuePair<string, Guid> GetContextKey(Message message)
    {
        var key = message.ReplyToMessage is not null
            ? $"{message.Chat.Id}_{message.ReplyToMessage.MessageId}"
            : $"{message.Chat.Id}_{message.MessageId}";

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
        var key = $"{answer.Chat.Id}_{answer.MessageId}";
        contextKeysService.SetValue(key, prevKey.Value);
        contextKeysService.RemoveValue(prevKey.Key);
    }

    private static string? GetMessageText(Message message)
        => message.Type switch
        {
            MessageType.Text => message.Text,
            MessageType.Photo => message.Caption,
            _ => string.Empty
        };

    private async Task<BinaryData?> GetPhotoFromMessage(Message message, CancellationToken cancellationToken)
    {
        var fileId = message.Photo?[^1].FileId;
        if (fileId != null)
        {
            var imageFile = await botClient.GetFile(fileId, cancellationToken);
            if (!string.IsNullOrEmpty(imageFile.FilePath))
            {
                using var imageStream = new MemoryStream();
                await botClient.DownloadFile(imageFile.FilePath, imageStream, cancellationToken);
                return new BinaryData(imageStream.ToArray());
            }
        }

        return null;
    }
}