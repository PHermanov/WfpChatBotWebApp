using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class EchoCommand : CommandBase, IRequest
{
    public string Text { get; }

    public EchoCommand(Message message) : base(message)
        => Text = message.GetAllParamText();
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
        var text = string.IsNullOrEmpty(request.Text) 
            ? await _textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken) 
            : request.Text;

        await _botClient.TrySendTextMessageAsync(request.ChatId, text, cancellationToken: cancellationToken);
    }
}