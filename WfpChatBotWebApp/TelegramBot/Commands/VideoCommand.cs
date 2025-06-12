using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class VideoCommand(Message message) : CommandWithPrompt(message), IRequest
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
        var answerMessage = await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            text: "...",
            parseMode: ParseMode.Markdown,
            replyToMessageId: request.MessageId,
            logger: logger,
            cancellationToken: cancellationToken);
        
        if (answerMessage == null)
            return;
        
        try
        {
            if (string.IsNullOrEmpty(request.Prompt))
            {
                logger.LogInformation("Video request is empty");

                var responsePhrase = await messageService.GetMessageByNameAsync(
                    TextMessageService.TextMessageNames.WhatWanted, 
                    cancellationToken);

                if (string.IsNullOrEmpty(responsePhrase))
                    return;
                
                await botClient.TryEditMessageTextAsync(
                    chatId: answerMessage.Chat.Id,
                    messageId: answerMessage.MessageId,
                    parseMode: ParseMode.Markdown,
                    text: $"*{responsePhrase}*",
                    logger: logger,
                    cancellationToken: cancellationToken);
            }
            else
            {
                var stream = await soraService.GetVideo(request.Prompt, 10, cancellationToken);

                if (stream != null)
                {
                    await botClient.TryEditMessageMedia(
                        chatId: answerMessage.Chat.Id,
                        messageId: answerMessage.MessageId,
                        media: new InputMediaAnimation(InputFile.FromStream(stream, "file.mp4")),
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
            
            await botClient.TryEditMessageTextAsync(
                chatId: answerMessage.Chat.Id,
                messageId: answerMessage.MessageId,
                parseMode: ParseMode.Markdown,
                text: message,
                logger: logger,
                cancellationToken: cancellationToken);
        }
    }
}