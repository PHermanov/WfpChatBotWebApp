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

public class RedrawCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "redraw";
    public Message? SourceMessage { get; } = message.Photo is { Length: > 0 }
        ? message
        : message.ReplyToMessage?.Photo is { Length: > 0 } ? message.ReplyToMessage : null;
}

public class RedrawCommandHandler(
    ITelegramBotClient botClient,
    ITextMessageService messageService,
    IAiImageEditService imageService,
    ILogger<RedrawCommandHandler> logger) : IRequestHandler<RedrawCommand>
{
    public async Task Handle(RedrawCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Param))
        {
            await SendMessage(request, Messages.RedrawUsage, cancellationToken);
            return;
        }
        if (request.SourceMessage is null)
        {
            await SendMessage(request, Messages.ImageSourceMissing, cancellationToken);
            return;
        }

        try
        {
            var source = await botClient.GetPhotoFromMessage(request.SourceMessage, cancellationToken);
            if (source is null)
            {
                await SendMessage(request, Messages.ImageSourceMissing, cancellationToken);
                return;
            }
            var sent = false;
            await foreach (var bytes in imageService.EditImage(request.Param, source, cancellationToken: cancellationToken))
            {
                if (bytes.Length == 0)
                    throw new InvalidOperationException("Image editing returned an empty image.");
                using var stream = new MemoryStream(bytes);
                var response = await botClient.TrySendPhotoAsync(logger, request.ChatId,
                    InputFile.FromStream(stream, "redraw.png"), ParseMode.Html,
                    replyToMessageId: request.MessageId, cancellationToken: cancellationToken);
                if (response is null)
                    throw new InvalidOperationException("The edited image could not be delivered.");
                sent = true;
            }
            if (!sent)
                throw new InvalidOperationException("Image editing returned no output.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            await SendMessage(request, Messages.ImageInputInvalid, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning("Image editing failed in chat {ChatId}: {ErrorType}", request.ChatId, e.GetType().Name);
            await SendMessage(request, Messages.ImageEditFailed, cancellationToken);
        }
    }

    private async Task SendMessage(RedrawCommand request, string name, CancellationToken cancellationToken)
    {
        var text = await messageService.GetMessageByNameAsync(name, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            text = await messageService.GetMessageByNameAsync(Messages.WhatWanted, cancellationToken);
        if (!string.IsNullOrWhiteSpace(text))
            await botClient.TrySendTextMessageAsync(request.ChatId, text, logger, ParseMode.Html,
                replyToMessageId: request.MessageId, cancellationToken: cancellationToken);
    }
}
