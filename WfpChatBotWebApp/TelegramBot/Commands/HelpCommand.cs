using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class HelpCommand(Message message) : CommandBase(message), IRequest;

public class HelpCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService)
    : IRequestHandler<HelpCommand>
{
    public async Task Handle(HelpCommand request, CancellationToken cancellationToken)
    {
        var message = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WhatWanted, cancellationToken);

        if (string.IsNullOrEmpty(message))
            return;
        
        var text = $"{request.FromMention} *{message}*";

        await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            parseMode: ParseMode.Markdown,
            text: text,
            cancellationToken: cancellationToken);
    }
}