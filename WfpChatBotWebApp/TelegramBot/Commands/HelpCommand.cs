using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class HelpCommand(Message message) : CommandWithParam(message), IRequest;

public class HelpCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService)
    : IRequestHandler<HelpCommand>
{
    public async Task Handle(HelpCommand request, CancellationToken cancellationToken)
    {
        var responsePhrase = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WhatWanted, cancellationToken);

        if (string.IsNullOrEmpty(responsePhrase))
            return;
        
        await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            replyToMessageId: request.MessageId,
            text: $"*{responsePhrase}*",
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }
}