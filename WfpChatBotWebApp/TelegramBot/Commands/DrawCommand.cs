using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class DrawCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "draw";
}

public class DrawCommandHandler(
        ITelegramBotClient botClient,
        ITextMessageService messageService,
        IOpenAiService openAiService,
        ILogger<DrawCommandHandler> logger)
    : IRequestHandler<DrawCommand>
{
    public async Task Handle(DrawCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Param))
            {
                logger.LogInformation("Draw request is empty");

                var responsePhrase = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WhatWanted, cancellationToken);

                if (string.IsNullOrEmpty(responsePhrase))
                    return;

                await botClient.TrySendTextMessageAsync(
                    chatId: request.ChatId,
                    replyToMessageId: request.MessageId,
                    text: $"*{responsePhrase}*",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);

                return;
            }

            var param = CutParameters(request.Param, out var imagesCount);

            await foreach (var url in openAiService.CreateImage(param, imagesCount, cancellationToken))
            {
                await botClient.TrySendPhotoAsync(
                    chatId: request.ChatId,
                    logger: logger,
                    photo: InputFile.FromUri(url),
                    replyToMessageId: request.MessageId,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name} for {ChatId}, Exception: {e}", nameof(DrawCommandHandler), request.ChatId, e);

            var message = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.FuckOff, cancellationToken);
            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: message,
                replyToMessageId: request.MessageId,
                cancellationToken: cancellationToken);
        }
    }
    
    private static string CutParameters(string param, out int imagesCount)
    {
        imagesCount = 1;

        var split = param.Split(" ");
        var countPart = split[0];

        if (countPart.Contains('(') && countPart.Contains(')'))
        {
            if (int.TryParse(countPart.Trim(' ', '(', ')'), out imagesCount))
            {
                if (imagesCount > 10)
                    imagesCount = 10;

                return string.Join(" ", split[1..]);
            }
        }

        return param;
    }
}