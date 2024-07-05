using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class MeCommand(Message message) : CommandWithParam(message), IRequest;

public class MeCommandHandler(
    ITelegramBotClient botClient,
    ITextMessageService textMessageService,
    ILogger<MeCommandHandler> logger)
    : IRequestHandler<MeCommand>
{
    public async Task Handle(MeCommand request, CancellationToken cancellationToken)
    {
        string reply;
        if (string.IsNullOrWhiteSpace(request.Param))
        {
            var phrase = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WhatWanted, cancellationToken);
            reply = $"{phrase} *{request.FromMention}*";
        }
        else
        {
            reply = $"{request.FromMention} *{request.Param}*";
        }

        try
        {
            await botClient.DeleteMessageAsync(request.ChatId, request.MessageId, cancellationToken);
        }
        catch (Exception)
        {
            logger.LogError("Can not delete message");
            return;
        }

        await botClient.TrySendTextMessageAsync(request.ChatId, reply, ParseMode.Markdown, cancellationToken: cancellationToken);
    }
}
