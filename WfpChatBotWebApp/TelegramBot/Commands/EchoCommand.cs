using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class EchoCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "echo";
}

public class EchoCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService)
    : IRequestHandler<EchoCommand>
{
    public async Task Handle(EchoCommand request, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrEmpty(request.Param)
            ? await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WhatWanted, cancellationToken)
            : request.Param;

        await botClient.TrySendTextMessageAsync(request.ChatId, text, cancellationToken: cancellationToken);
    }
}