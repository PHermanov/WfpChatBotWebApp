﻿using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WfpChatBotWebApp.TelegramBot;

public static class TelegramBotClientExtensions
{
    public static async Task TrySendTextMessageAsync(this ITelegramBotClient client,
        ChatId chatId,
        string text,
        ParseMode parseMode = ParseMode.Html,
        bool disableWebPagePreview = false,
        bool disableNotification = false,
        int replyToMessageId = 0,
        IReplyMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                parseMode: parseMode,
                disableWebPagePreview: disableWebPagePreview,
                disableNotification: disableNotification,
                replyToMessageId: replyToMessageId,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch
        {
        }
    }
}

