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
        try
        {
            var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
            logger.LogInformation("KolodaJobHandler for chats: {Chats} at {Now}", string.Join(',', allChatIds), DateTime.UtcNow);

            if (allChatIds.Length == 0)
                return;

            var httpClient = httpClientFactory.CreateClient("Pictures");
           
            for (var i = 0; i < allChatIds.Length; i++)
            {
                try
                {
                    var imageStream = await httpClient.GetStreamAsync("koloda.jpg" + configuration.GetValue<string>("StickerSas"), cancellationToken);
                    await botClient.TrySendPhotoAsync(
                        logger: logger,
                        parseMode: ParseMode.Html,
                        chatId: allChatIds[i],
                        photo: InputFile.FromStream(imageStream),
                        cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Exception in KolodaJobHandler for {ChatId}", allChatIds[i]);
                    continue;
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "KolodaJobHandler Exception");
            return;
        }
    }
}