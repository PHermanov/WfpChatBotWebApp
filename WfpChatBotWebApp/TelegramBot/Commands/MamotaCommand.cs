﻿using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class MamotaCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "mamota";
}

public class MamotaCommandHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ITextMessageService messageService,
    IStickerService stickerService, 
    IRandomService randomService,
    ILogger<MamotaCommandHandler> logger)
    : IRequestHandler<MamotaCommand>
{
    public async Task Handle(MamotaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var users = await repository.GetActiveUsersForChatAsync(request.ChatId, cancellationToken);

            if (users.Length == 0)
                return;

            var random = await randomService.GetRandomNumber(users.Length);
            var randomUser = users[random];

            var textTemplate = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.MamotaSays, cancellationToken);

            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: string.Format(textTemplate, randomUser.GetUserMention()),
                parseMode: ParseMode.Markdown,
                logger: logger,
                cancellationToken: cancellationToken);

            var stickerUrl = await stickerService.GetRandomStickerFromSet(StickerService.StickerSet.Mamota, cancellationToken);

            if (string.IsNullOrWhiteSpace(stickerUrl))
                return;
            
            await botClient.TrySendStickerAsync(
                chatId: request.ChatId,
                sticker: InputFile.FromUri(stickerUrl),
                logger: logger,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Mamota command failed");
        }
    }
}