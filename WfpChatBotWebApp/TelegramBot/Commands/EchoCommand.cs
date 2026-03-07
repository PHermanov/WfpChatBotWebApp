using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class EchoCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "echo";
}

public class EchoCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService, ILogger<EchoCommandHandler> logger)
    : IRequestHandler<EchoCommand>
{
    public async Task Handle(EchoCommand request, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrEmpty(request.Param)
            ? await textMessageService.GetMessageByNameAsync(Messages.WhatWanted, cancellationToken)
            : request.Param;

        await botClient.TrySendTextMessageAsync(request.ChatId, text, logger: logger, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
    }
}