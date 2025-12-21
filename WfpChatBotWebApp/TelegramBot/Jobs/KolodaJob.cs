using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class KolodaJobRequest : IRequest;

public class KolodaJobHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<KolodaJobRequest> logger)
    : IRequestHandler<KolodaJobRequest>
{
    public async Task Handle(KolodaJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} at {Now}", nameof(KolodaJobHandler), DateTime.UtcNow);

        try
        {
            var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
            logger.LogInformation("{Name} For chats: {Chats} ", nameof(KolodaJobHandler), string.Join(',', allChatIds));

            if (allChatIds.Length == 0)
            {
                logger.LogError("{Name}, no chats found", nameof(KolodaJobHandler));
                return;
            }

            var httpClient = httpClientFactory.CreateClient("Pictures");
            var imageStream = await httpClient.GetStreamAsync("koloda.jpg" + configuration.GetValue<string>("StickerSas"), cancellationToken);
            var inputFile = InputFile.FromStream(imageStream);

            for (var i = 0; i < allChatIds.Length; i++)
            {
                try
                {
                    await botClient.TrySendPhotoAsync(
                        logger: logger,
                        chatId: allChatIds[i],
                        photo: inputFile,
                        caption: string.Empty,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Exception  in {Name} for {ChatId}", nameof(KolodaJobHandler), allChatIds[i]);
                    continue;
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name}, Exception {e}", nameof(KolodaJobHandler), e);
            return;
        }
    }
}