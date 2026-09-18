using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class DrawCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "draw";
}

public class DrawCommandHandler(
        ITelegramBotClient botClient,
        ITextMessageService messageService,
        IAiImageService aiImageService,
        ILogger<DrawCommandHandler> logger)
    : IRequestHandler<DrawCommand>
{
    public async Task Handle(DrawCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Param))
            {
                var responsePhrase = await messageService.GetMessageByNameAsync(Messages.WhatWanted, cancellationToken);

                if (string.IsNullOrEmpty(responsePhrase))
                    return;

                await botClient.TrySendTextMessageAsync(
                    chatId: request.ChatId,
                    replyToMessageId: request.MessageId,
                    text: $"*{responsePhrase}*",
                    parseMode: ParseMode.Markdown,
                    logger: logger,
                    cancellationToken: cancellationToken);

                return;
            }

            await foreach (var bytes in aiImageService.CreateImage(request.Param, cancellationToken: cancellationToken))
            {
                using var stream = new MemoryStream(bytes);
                await botClient.TrySendPhotoAsync(
                    chatId: request.ChatId,
                    logger: logger,
                    photo: InputFile.FromStream(stream, "draw.png"),
                    parseMode: ParseMode.Html,
                    replyToMessageId: request.MessageId,
                    cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "DrawCommandHandler for {ChatId}", request.ChatId);

            var message = await messageService.GetMessageByNameAsync(Messages.FuckOff, cancellationToken);
            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: message,
                parseMode: ParseMode.Html,
                replyToMessageId: request.MessageId,
                logger: logger,
                cancellationToken: cancellationToken);
        }
    }
}