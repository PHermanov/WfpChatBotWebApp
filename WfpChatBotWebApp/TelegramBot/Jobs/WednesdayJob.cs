using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

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
        logger.LogInformation("{Name} at {Now}", nameof(WednesdayJobHandler), DateTime.UtcNow);

        try
        {
            var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
            logger.LogInformation("{Name} For chats: {Chats} ", nameof(WednesdayJobHandler), string.Join(',', allChatIds));

            if (allChatIds.Length == 0)
            {
                logger.LogError("{Name}, no chats found", nameof(WednesdayJobHandler));
                return;
            }

            var message = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WednesdayMyDudes, cancellationToken);
            if (string.IsNullOrWhiteSpace(message))
            {
                logger.LogError("{Name}, message template not found", nameof(WednesdayJobHandler));
                return;
            }

            for (var i = 0; i < allChatIds.Length; i++)
            {
                try
                {
                    await botClient.TrySendTextMessageAsync(
                        chatId: allChatIds[i],
                        text: message,
                        cancellationToken: cancellationToken);

                    var stickerUrl = await stickerService.GetRandomStickerFromSet(StickerService.StickerSet.Frog, cancellationToken);
                    if (string.IsNullOrWhiteSpace(stickerUrl))
                        return;

                    await botClient.TrySendStickerAsync(
                        chatId: allChatIds[i],
                        sticker: InputFile.FromUri(stickerUrl),
                        cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError("{Name} for {ChatId}, Exception  {e}", nameof(WednesdayJobHandler), allChatIds[i], e);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name}, Exception  {e}", nameof(WednesdayJobHandler), e);
        }
    }
}