using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class MonthlyWinnerJobRequest : IRequest;

public class MonthlyWinnerJobHandler(
    ITelegramBotClient botClient,
    ITextMessageService textMessageService,
    IGameRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<MonthlyWinnerJobRequest> logger)
    : IRequestHandler<MonthlyWinnerJobRequest>
{
    public async Task Handle(MonthlyWinnerJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} at {Now}", nameof(MonthlyWinnerJobHandler), DateTime.UtcNow);

        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
        logger.LogInformation("{Name} For chats: {Chats} ", nameof(MonthlyWinnerJobHandler), string.Join(',', allChatIds));
        
        if (allChatIds.Length == 0)
        {
            logger.LogError("{Name}, no chats found", nameof(MonthlyWinnerJobHandler));
            return;
        }
        
        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImage = await httpClient.GetStreamAsync("bowl.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);
        
        logger.LogInformation("{Name} Downloaded bowl image", nameof(MonthlyWinnerJobHandler));
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessMonthlyWinnerForChat(allChatIds[i], bowlImage, cancellationToken);
        }
    }

    private async Task ProcessMonthlyWinnerForChat(long chatId, Stream bowlImageStream, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} for {ChatId}", nameof(MonthlyWinnerJobHandler), chatId);

        try
        {
            var monthWinner = await repository.GetWinnerForMonthAsync(chatId, DateTime.Now, cancellationToken);

            logger.LogInformation("{Name} for {ChatId}, Month winner {winner}", nameof(MonthlyWinnerJobHandler), chatId, monthWinner?.UserId);

            if (monthWinner != null)
            {
                var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);
                var mention = users
                    .FirstOrDefault(u => u.UserId == monthWinner.UserId)!
                    .GetUserMention();

                var monthWinnerMessage = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.MonthWinner, cancellationToken);
                var congratsMessage = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.Congrats, cancellationToken);

                var message = $"{monthWinnerMessage}{Environment.NewLine}\u269C {mention} \u269C{Environment.NewLine}{congratsMessage}";

                UserProfilePhotos? userProfilePhotos = null;
                try
                {
                    logger.LogInformation("{Name} for {ChatId}, Loading user photos", nameof(MonthlyWinnerJobHandler), chatId);
                    userProfilePhotos = await botClient.GetUserProfilePhotosAsync(monthWinner.UserId, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError("{Name} for {ChatId}, Loading user photos. Exception: {exception}", nameof(MonthlyWinnerJobHandler), chatId, e.Message);
                }

                if (userProfilePhotos == null || userProfilePhotos.Photos.Length == 0)
                {
                    logger.LogInformation("{Name} for {ChatId}, photos not loaded", nameof(MonthlyWinnerJobHandler), chatId);

                    await botClient.TrySendPhotoAsync(
                        logger: logger,
                        chatId: chatId,
                        photo: InputFile.FromStream(bowlImageStream),
                        caption: message,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    logger.LogInformation("{Name} for {ChatId}, photo loaded", nameof(MonthlyWinnerJobHandler), chatId);

                    var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
                    var photoFile = await botClient.GetFileAsync(photoSize?.FileId ?? string.Empty, cancellationToken);

                    logger.LogInformation("{Name} for {ChatId}, photo file info loaded", nameof(MonthlyWinnerJobHandler), chatId);

                    var avatarStream = new MemoryStream();
                    await botClient.DownloadFileAsync(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);

                    logger.LogInformation("{Name} for {ChatId}, photo file downloaded", nameof(MonthlyWinnerJobHandler), chatId);

                    var winnerImage = await ImageProcessor.GetWinnerImageMonth(bowlImageStream, avatarStream, DateTime.Today);

                    logger.LogInformation("{Name} for {ChatId}, winner image created. Sending", nameof(MonthlyWinnerJobHandler), chatId);

                    await botClient.TrySendPhotoAsync(
                        logger: logger,
                        chatId: chatId,
                        photo: InputFile.FromStream(winnerImage),
                        caption: message,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                logger.LogError("{Name} for {ChatId}, Winner not selected", nameof(MonthlyWinnerJobHandler), chatId);
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name} for {ChatId}, Exception: {e}", nameof(MonthlyWinnerJobHandler), chatId, e);
        }
    }
}