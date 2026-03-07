using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class WednesdayJobRequest : IRequest;

public class WednesdayJobHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    IStickerService stickerService,
    ITextMessageService messageService,
    ILogger<MonthlyWinnerJobRequest> logger)
    : IRequestHandler<WednesdayJobRequest>
{
    public async Task Handle(WednesdayJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("WednesdayJobHandler: at {Now}", DateTime.UtcNow);

        try
        {
            var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
            logger.LogInformation("WednesdayJobHandler: For chats: {Chats} ", string.Join(',', allChatIds));

            if (allChatIds.Length == 0)
            {
                logger.LogError("WednesdayJobHandler: no chats found");
                return;
            }

            var message = await messageService.GetMessageByNameAsync(Messages.WednesdayMyDudes, cancellationToken);
            if (string.IsNullOrWhiteSpace(message))
            {
                logger.LogError("WednesdayJobHandler: message template not found");
                return;
            }

            for (var i = 0; i < allChatIds.Length; i++)
            {
                try
                {
                    await botClient.TrySendTextMessageAsync(
                        chatId: allChatIds[i],
                        text: message,
                        parseMode: ParseMode.Html,
                        logger: logger,
                        cancellationToken: cancellationToken);

                    var stickerUrl = await stickerService.GetRandomStickerFromSet(StickerService.StickerSet.Frog, cancellationToken);
                    if (string.IsNullOrWhiteSpace(stickerUrl))
                        return;

                    await botClient.TrySendStickerAsync(
                        chatId: allChatIds[i],
                        sticker: InputFile.FromUri(stickerUrl),
                        logger: logger,
                        cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Exception  in WednesdayJobHandler for {ChatId}", allChatIds[i]);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("WednesdayJobHandler: Exception {e}", e);
        }
    }
}