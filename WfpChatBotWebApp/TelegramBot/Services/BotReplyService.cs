using System.Text;
using System.Web;
using Telegram.Bot;
using Telegram.Bot.Types;
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
    ILogger<BotReplyService> logger)
    : IBotReplyService
{
    private readonly Dictionary<string, Guid> _contextKeys = new();

    public async Task Reply(string mention, Message message, CancellationToken cancellationToken)
    {
        var request = !string.IsNullOrEmpty(mention)
            ? message.Text!.Replace($"@{mention}", string.Empty).Trim()
            : message.Text!.Trim();

        var answerMessage = await botClient.TrySendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "...",
            replyToMessageId: message.MessageId,
            cancellationToken: cancellationToken);

        if (answerMessage == null)
            return;

        var responseBuffer = new StringBuilder();
        var previousBufferLength = 0;

        try
        {
            var contextKey = GetContextKey(message);

            await foreach (var part in openAiService.ProcessMessage(contextKey.Value, request, cancellationToken))
            {
                responseBuffer.Append(HttpUtility.HtmlEncode(part));

                if (responseBuffer.Length - previousBufferLength >= 60)
                {
                    previousBufferLength = responseBuffer.Length;

                    await botClient.TryEditMessageTextAsync(
                        chatId: answerMessage.Chat.Id,
                        messageId: answerMessage.MessageId,
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
                cancellationToken: cancellationToken);
        }
    }

    private KeyValuePair<string, Guid> GetContextKey(Message message)
    {
        var key = message.ReplyToMessage is not null
            ? $"{message.Chat.Id}_{message.ReplyToMessage.MessageId}"
            : $"{message.Chat.Id}_{message.MessageId}";

        if (_contextKeys.TryGetValue(key, out var contextKey))
        {
            return new KeyValuePair<string, Guid>(key, contextKey);
        }
        else
        {
            var newContextKey = Guid.NewGuid();

            _contextKeys[key] = newContextKey;

            return new(key, newContextKey);
        }
    }

    private void SetContextKey(Message answer, KeyValuePair<string, Guid> prevKey)
    {
        var key = $"{answer.Chat.Id}_{answer.MessageId}";

        _contextKeys[key] = prevKey.Value;

        _contextKeys.Remove(prevKey.Key);
    }
}