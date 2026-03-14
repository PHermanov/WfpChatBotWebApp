using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.InternetSearch;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class GoogleCommand(Message message) : CommandWithParam(message), IRequest
{
    public override string Name => "google";
}

public class GoogleCommandHandler(
    ITelegramBotClient botClient,
    IInternetSearchService internetSearchService,
    ITextMessageService textMessageService,
    ILogger<GoogleCommandHandler> logger)
    : IRequestHandler<GoogleCommand>
{
    public async Task Handle(GoogleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Param))
        {
            var responsePhrase = await textMessageService.GetMessageByNameAsync(Messages.WhatWanted, cancellationToken);

            if (string.IsNullOrEmpty(responsePhrase))
                return;

            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                replyToMessageId: request.MessageId,
                text: $"*{responsePhrase}*",
                logger: logger,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);

            return;
        }

        var results = await internetSearchService.Search(request.Param.Trim(), 3, cancellationToken);

        if (results == null || results.Length == 0)
            return;

        foreach (var result in results)
        {
            var msg = $"{result.Title}{Environment.NewLine}{result.Link}";

            await botClient.TrySendTextMessageAsync(request.ChatId, msg, parseMode: ParseMode.Html, logger: logger, cancellationToken: cancellationToken);
        }
    }
}

