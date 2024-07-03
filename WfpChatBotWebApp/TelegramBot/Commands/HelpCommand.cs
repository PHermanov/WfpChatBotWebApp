using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class HelpCommand(Message message) : CommandBase(message), IRequest;

public class HelpCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService)
    : IRequestHandler<HelpCommand>
{
    public async Task Handle(HelpCommand request, CancellationToken cancellationToken)
    {
        var message = await textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken);
        await botClient.TrySendTextMessageAsync(request.ChatId, message, cancellationToken: cancellationToken);
    }
}