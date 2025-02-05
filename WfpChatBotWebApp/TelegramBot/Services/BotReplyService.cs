using System.Text;
using System.Web;
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
        var text = message.Type switch
        {
            MessageType.Text => message.Text,
            MessageType.Photo => message.Caption,
            _ => string.Empty
        };
        
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
            cancellationToken: cancellationToken);

        if (answerMessage == null)
            return;

        var responseBuffer = new StringBuilder();
        var previousBufferLength = 0;

        try
        {
            var contextKey = GetContextKey(message);

            BinaryData? imageBinary = null;
            if (message.Photo != null)
            {
                var imageFile = await botClient.GetFile(message.Photo[^1].FileId, cancellationToken);
                if (!string.IsNullOrEmpty(imageFile.FilePath))
                {
                    using var imageStream = new MemoryStream();
                    await botClient.DownloadFile(imageFile.FilePath, imageStream, cancellationToken);
                    imageBinary = new BinaryData(imageStream.ToArray());
                }
            }
            
            await foreach (var part in openAiService.ProcessMessage(contextKey.Value, request, imageBinary, cancellationToken))
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
                        cancellationToken: cancellationToken);

                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }

            SetContextKey(answerMessage, contextKey);
        }
        catch (Exception e)
        {
            logger.LogError("{Class} Exception: {e}", nameof(BotReplyService), e);

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
}