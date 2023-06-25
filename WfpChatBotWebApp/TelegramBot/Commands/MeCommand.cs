using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class MeCommand : CommandWithParam, IRequest
{
    public MeCommand(Message message) : base(message) { }
}

public class MeCommandHandler : IRequestHandler<MeCommand>
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITextMessageService _textMessageService;
    private readonly ILogger<MeCommandHandler> _logger;

    public MeCommandHandler(ITelegramBotClient botClient, ITextMessageService textMessageService, ILogger<MeCommandHandler> logger)
    {
        _botClient = botClient;
        _textMessageService = textMessageService;
        _logger = logger;
    }

    public async Task Handle(MeCommand request, CancellationToken cancellationToken)
    {
        string reply;
        if (string.IsNullOrWhiteSpace(request.Param))
        {
            reply = await _textMessageService.GetMessageByNameAsync(TextMessageNames.WhatWanted, cancellationToken);
        }
        else
        {
            reply = $"{request.FromMention} *{request.Param}*";
        }

        try
        {
            await _botClient.DeleteMessageAsync(request.ChatId, request.MessageId, cancellationToken);
        }
        catch (Exception)
        {
            _logger.LogError("Can not delete message");
            return;
        }

        await _botClient.TrySendTextMessageAsync(request.ChatId, reply, ParseMode.Markdown, cancellationToken: cancellationToken);
    }
}
