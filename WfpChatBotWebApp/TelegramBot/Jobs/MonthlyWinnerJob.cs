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
        logger.LogInformation("Executed {Name} at {Now}", nameof(MonthlyWinnerJobRequest), DateTime.UtcNow);

        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);

        logger.LogInformation("Got {Chats} chats", string.Join(',', allChatIds));

        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImage = await httpClient.GetStreamAsync("bowl.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessMonthlyWinnerForChat(allChatIds[i], bowlImage, cancellationToken);
        }
    }

    private async Task ProcessMonthlyWinnerForChat(long chatId, Stream bowlImageStream, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed {Name} for {ChatId}", nameof(ProcessMonthlyWinnerForChat), chatId);

        try
        {
            var monthWinner = await repository.GetWinnerForMonthAsync(chatId, DateTime.Now, cancellationToken);

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
                bowlImageStream.Seek(0, SeekOrigin.Begin);
                
                try
                {
                    userProfilePhotos = await botClient.GetUserProfilePhotosAsync(monthWinner.UserId, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError("Executed {Name} for {ChatId}, Exception {exception}", nameof(ProcessMonthlyWinnerForChat), chatId, e.Message);
                }
                
                if (userProfilePhotos == null || userProfilePhotos.Photos.Length == 0)
                {
                    await botClient.TrySendPhotoAsync(
                        chatId,
                        InputFile.FromStream(bowlImageStream),
                        message,
                        ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
                    var photoFile = await botClient.GetFileAsync(photoSize?.FileId ?? string.Empty, cancellationToken);

                    var avatarStream = new MemoryStream();
                    await botClient.DownloadFileAsync(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);
                    avatarStream.Position = 0;
                
                    var winnerImage = await ImageProcessor.GetWinnerImageMonth(bowlImageStream, avatarStream, DateTime.Today);

                    await botClient.TrySendPhotoAsync(
                        chatId,
                        InputFile.FromStream(winnerImage),
                        message,
                        ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("Executed {Name} for {ChatId}, Exception {exception}", nameof(ProcessMonthlyWinnerForChat), chatId, e.Message);
        }
    }
}