using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class HelpCommand : CommandBase, IRequest
{
    public HelpCommand(Message message) : base(message) { }
}

public class HelpCommandHandler : IRequestHandler<HelpCommand>
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITextMessageService _textMessageService;

    public HelpCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService)
    {
        _botClient = botClient;
        _textMessageService = textMessageService;
    }

    public async Task Handle(HelpCommand request, CancellationToken cancellationToken)
    {
        var message = await _textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken);
        await _botClient.TrySendTextMessageAsync(request.ChatId, message, cancellationToken: cancellationToken);
    }
}