using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class VideoCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "video";
}

public class VideoCommandHandler(
    ITelegramBotClient botClient,
    ITextMessageService messageService,
    ISoraService soraService,
    ILogger<DrawCommandHandler> logger)
    : IRequestHandler<VideoCommand>
{
    public async Task Handle(VideoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Param))
            {
                logger.LogInformation("Video request is empty");

                var responsePhrase = await messageService.GetMessageByNameAsync(
                    TextMessageService.TextMessageNames.WhatWanted, 
                    cancellationToken);

                if (string.IsNullOrEmpty(responsePhrase))
                    return;

                await botClient.TrySendTextMessageAsync(
                    chatId: request.ChatId,
                    replyToMessageId: request.MessageId,
                    text: $"*{responsePhrase}*",
                    parseMode: ParseMode.Markdown,
                    logger: logger,
                    cancellationToken: cancellationToken);
            }
            else
            {
                var stream = await soraService.GetVideo(request.Param, 10, cancellationToken);

                if (stream != null)
                {
                    await botClient.TrySendAnimationAsync(
                        chatId: request.ChatId,
                        replyToMessageId: request.MessageId,
                        video: InputFile.FromStream(stream, "file.mp4"),
                        logger: logger,
                        cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name} for {ChatId}, Exception: {e}", nameof(VideoCommandHandler), request.ChatId, e);

            var message = await messageService.GetMessageByNameAsync(
                TextMessageService.TextMessageNames.FuckOff, 
                cancellationToken);
            
            if (string.IsNullOrEmpty(message))
                return;
            
            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: message,
                replyToMessageId: request.MessageId,
                logger: logger,
                cancellationToken: cancellationToken);
        }
    }
}