using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class EchoCommand : CommandWithParam, IRequest
{
    public EchoCommand(Message message) : base(message) { }
}

public class EchoCommandHandler : IRequestHandler<EchoCommand>
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITextMessageService _textMessageService;

    public EchoCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService)
    {
        _botClient = botClient;
        _textMessageService = textMessageService;
    }

    public async Task Handle(EchoCommand request, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrEmpty(request.Param)
            ? await _textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken)
            : request.Param;

        await _botClient.TrySendTextMessageAsync(request.ChatId, text, cancellationToken: cancellationToken);
    }
}