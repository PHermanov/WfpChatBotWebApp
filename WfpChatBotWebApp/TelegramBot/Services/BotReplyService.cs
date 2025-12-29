using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IBotReplyService
{
    Task Reply(Message message, CancellationToken cancellationToken);
}

public class BotReplyService(
    ITelegramBotClient botClient,
    IOpenAiService openAiService,
    ITextMessageService messageService,
    IContextKeysService contextKeysService,
    ILogger<BotReplyService> logger)
    : IBotReplyService
{
    public async Task Reply(Message message, CancellationToken cancellationToken)
    {
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
            var requests = new List<OpenAiRequest>();
            
            if (message.ReplyToMessage != null)
            {
                // Check if the reply message is not the last message in context
                if (!contextKeysService.ContainsKey($"{message.Chat.Id}_{message.ReplyToMessage.MessageId}"))
                {
                    requests.Add(await CreateRequest(message.ReplyToMessage, cancellationToken));
                }
            }
            
            requests.Add(await CreateRequest(message, cancellationToken));
            
            var contextKey = GetContextKey(message);
            
            await foreach (var part in openAiService.ProcessMessage(contextKey.Value, message.Chat.Id, requests, cancellationToken))
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

    private async ValueTask<OpenAiRequest> CreateRequest(
        Message message,
        CancellationToken cancellationToken) =>
        new()
        {
            UserId = message.From?.Id,
            MessageText = message.GetMessageText(),
            Image = await botClient.GetPhotoFromMessage(message, cancellationToken)
        };

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