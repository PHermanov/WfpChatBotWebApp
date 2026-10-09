using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public static class TelegramThreadKey
{
    public static string GetMessageKey(long chatId, int messageId) => $"{chatId}_{messageId}";

    public static string GetThreadKey(Message message)
    {
        var threadId = message.MessageThreadId ?? message.ReplyToMessage?.MessageThreadId;
        if (threadId is not null)
            return GetMessageKey(message.Chat.Id, threadId.Value);

        if (message.ReplyToMessage is null)
            return GetMessageKey(message.Chat.Id, message.MessageId);

        return GetMessageKey(message.Chat.Id, message.ReplyToMessage.MessageId);
    }
}
