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
        logger.LogInformation("{Name} at {Now}", nameof(MonthlyWinnerJobRequest), DateTime.UtcNow);

        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);

        logger.LogInformation("{Name} Got {Chats} chats", nameof(MonthlyWinnerJobRequest), string.Join(',', allChatIds));

        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImage = await httpClient.GetStreamAsync("bowl.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessMonthlyWinnerForChat(allChatIds[i], bowlImage, cancellationToken);
        }
    }

    private async Task ProcessMonthlyWinnerForChat(long chatId, Stream bowlImageStream, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} for {ChatId}", nameof(ProcessMonthlyWinnerForChat), chatId);

        try
        {
            var monthWinner = await repository.GetWinnerForMonthAsync(chatId, DateTime.Now, cancellationToken);

            logger.LogInformation("{Name} for {ChatId}, Month winner {winner}", nameof(ProcessMonthlyWinnerForChat), chatId, monthWinner?.UserId);

            if (monthWinner != null)
            {
                var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);
                var mention = users
                    .FirstOrDefault(u => u.UserId == monthWinner.UserId)!
                    .GetUserMention();

                var monthWinnerMessage = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.MonthWinner, cancellationToken);
                var congratsMessage = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.Congrats, cancellationToken);

                var message = $"{monthWinnerMessage}{Environment.NewLine}\u269C {mention} \u269C{Environment.NewLine}{congratsMessage}";

                bowlImageStream.Seek(0, SeekOrigin.Begin);

                UserProfilePhotos? userProfilePhotos = null;
                try
                {
                    logger.LogInformation("{Name} for {ChatId}, Loading user photos", nameof(ProcessMonthlyWinnerForChat), chatId);
                    userProfilePhotos = await botClient.GetUserProfilePhotosAsync(monthWinner.UserId, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError("{Name} for {ChatId}, Loading user photos. Exception {exception}", nameof(ProcessMonthlyWinnerForChat), chatId, e.Message);
                }

                if (userProfilePhotos == null || userProfilePhotos.Photos.Length == 0)
                {
                    logger.LogInformation("{Name} for {ChatId}, photos not loaded", nameof(ProcessMonthlyWinnerForChat), chatId);

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
                    logger.LogInformation("{Name} for {ChatId}, photo loaded", nameof(ProcessMonthlyWinnerForChat), chatId);

                    var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
                    var photoFile = await botClient.GetFileAsync(photoSize?.FileId ?? string.Empty, cancellationToken);

                    logger.LogInformation("{Name} for {ChatId}, photo file info loaded", nameof(ProcessMonthlyWinnerForChat), chatId);

                    var avatarStream = new MemoryStream();
                    await botClient.DownloadFileAsync(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);
                    avatarStream.Position = 0;

                    logger.LogInformation("{Name} for {ChatId}, photo file downloaded", nameof(ProcessMonthlyWinnerForChat), chatId);

                    var winnerImage = await ImageProcessor.GetWinnerImageMonth(bowlImageStream, avatarStream, DateTime.Today);

                    logger.LogInformation("{Name} for {ChatId}, winner image created. Sending", nameof(ProcessMonthlyWinnerForChat), chatId);

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
                logger.LogError("{Name} for {ChatId}, Winner not selected", nameof(ProcessMonthlyWinnerForChat), chatId);
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name} for {ChatId}, Exception {exceptionType} {exception}", nameof(ProcessMonthlyWinnerForChat), chatId, e.GetType(), e.Message);
        }
    }
}