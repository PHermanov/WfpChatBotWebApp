using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class MeCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "me";
}

public class MeCommandHandler(
    ITelegramBotClient botClient,
    ITextMessageService textMessageService,
    ILogger<MeCommandHandler> logger)
    : IRequestHandler<MeCommand>
{
    public async Task Handle(MeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Param))
        {
            logger.LogInformation("Param is empty");
            
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

        var reply = $"{request.FromMention} *{request.Param}*";

        try
        {
            await botClient.DeleteMessageAsync(request.ChatId, request.MessageId, cancellationToken);
        }
        catch (Exception)
        {
            logger.LogError("Can not delete message");
            return;
        }

        await botClient.TrySendTextMessageAsync(
            request.ChatId, 
            text: reply, 
            parseMode: ParseMode.Markdown, 
            cancellationToken: cancellationToken);
    }
}
