using System.Text;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.Persistence.Models;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class YearlyWinnerJobRequest : IRequest;

public class YearlyWinnerJobHandler(
    ITelegramBotClient botClient,
    ITextMessageService messageService,
    IGameRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<YearlyWinnerJobRequest> logger)
    : IRequestHandler<YearlyWinnerJobRequest>
{
    public async Task Handle(YearlyWinnerJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} at {Now}", nameof(YearlyWinnerJobRequest), DateTime.UtcNow);

        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
        logger.LogInformation("{Name} For chats: {Chats} ", nameof(YearlyWinnerJobRequest), string.Join(',', allChatIds));

        if (allChatIds.Length == 0)
        {
            logger.LogError("{Name}, no chats found", nameof(YearlyWinnerJobRequest));
            return;
        }

        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessYearlyWinnerForChat(allChatIds[i], cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    private async Task ProcessYearlyWinnerForChat(long chatId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{Name} for {ChatId}", nameof(YearlyWinnerJobRequest), chatId);

            var year = DateTime.Today.Year;
            var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);

            // welcome message
            var yearSummarizeMsg = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.YearSummarizeMsg, cancellationToken);
            if (!string.IsNullOrEmpty(yearSummarizeMsg))
            {
                logger.LogInformation("{Name} for {ChatId} sending welcome message", nameof(YearlyWinnerJobRequest), chatId);
                await botClient.TrySendTextMessageAsync(
                    chatId: chatId,
                    text: yearSummarizeMsg,
                    parseMode: ParseMode.Markdown,
                    logger: logger,
                    cancellationToken: cancellationToken);
            }

            // year winner by total count
            var yearWinnerByCount = await repository.GetYearWinnerByCountAsync(chatId, year, cancellationToken);
            var yearWinnerByCountMessageTemplate = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.YearByCountWinnerMsg, cancellationToken);

            if (yearWinnerByCount != null && !string.IsNullOrEmpty(yearWinnerByCountMessageTemplate))
            {
                logger.LogInformation("{Name} for {ChatId} YearWinnerByCount is {Winner}", nameof(YearlyWinnerJobRequest), chatId, yearWinnerByCount.UserName);

                var user = users.FirstOrDefault(u => u.UserId == yearWinnerByCount.UserId);
                if (user != null)
                {
                    if (user.Inactive)
                    {
                        var userMissing = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.UserMissing, cancellationToken);
                        yearWinnerByCount.UserName += userMissing;
                    }

                    var yearWinnerByCountMessage = string.Format(yearWinnerByCountMessageTemplate, user.UserName);
                    await botClient.TrySendTextMessageAsync(chatId, yearWinnerByCountMessage, logger: logger, cancellationToken: cancellationToken);
                }
                else
                {
                    logger.LogError("{Name} for {ChatId} YearWinnerByCount: user is null", nameof(YearlyWinnerJobRequest), chatId);
                }
            }

            await Task.Delay(500, cancellationToken);

            // year winner by months count

            // find winner of every month
            logger.LogInformation("{Name} for {ChatId} find winner of every month", nameof(YearlyWinnerJobRequest), chatId);
            var monthWinners = new Dictionary<int, PlayerCountViewModel>(12);
            for (var monthNumber = 1; monthNumber <= 12; monthNumber++)
            {
                var lastDay = new DateTime(year, monthNumber, DateTime.DaysInMonth(year, monthNumber));
                var monthWinner = await repository.GetWinnerForMonthAsync(chatId, lastDay, cancellationToken);

                if (monthWinner != null)
                {
                    monthWinners.Add(monthNumber, monthWinner);
                }
            }

            if (monthWinners.Count != 0)
            {
                // set all win counts for winners
                foreach (var monthWinner in monthWinners.Values)
                {
                    monthWinner.Count = monthWinners.Values.Count(w => w.UserId == monthWinner.UserId);
                }

                // get the top winner with max count
                var yearWinner = monthWinners.Values.MaxBy(c => c.Count);
                var yearWinnerUser = users.FirstOrDefault(u => u.UserId == yearWinner?.UserId);
                if (yearWinner != null && yearWinnerUser != null)
                {
                    logger.LogInformation("{Name} for {ChatId} YearWinner is {Winner}", nameof(YearlyWinnerJobRequest), chatId, yearWinnerUser.UserName);

                    // display winners of all months
                    var monthStrRes = new StringBuilder();
                    foreach (var (monthNumber, winner) in monthWinners.OrderBy(k => k.Key))
                    {
                        var monthName = await GetMonthName(monthNumber, cancellationToken);
                        var monthDisplay = string.IsNullOrEmpty(monthName) ? monthNumber.ToString() : monthName;
                        var monthWinnerUserName = users.FirstOrDefault(u => u.UserId == winner.UserId)?.UserName;
                        monthStrRes.AppendLine($"<i>{monthDisplay}</i>: <b>{monthWinnerUserName}</b>");
                    }

                    var yearAllMonthWinnersMsg = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.YearAllMonthWinnersMsg, cancellationToken);
                    if (!string.IsNullOrEmpty(yearAllMonthWinnersMsg) && monthStrRes.Length > 0)
                    {
                        await Task.Delay(500, cancellationToken);
                        await botClient.TrySendTextMessageAsync(chatId, yearAllMonthWinnersMsg, logger: logger, cancellationToken: cancellationToken);
                        await Task.Delay(500, cancellationToken);
                        await botClient.TrySendTextMessageAsync(chatId, monthStrRes.ToString(), logger: logger, cancellationToken: cancellationToken);
                    }

                    // check if there are more players with the same win count as leader
                    var anotherWinners = monthWinners.Values
                        .Where(w => w.Count == yearWinner.Count)
                        .Distinct()
                        .ToList();

                    if (anotherWinners.Count == 1)
                    {
                        await Task.Delay(500, cancellationToken);
                        var mainYearWinnerSingle = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.MainYearWinnerSingle, cancellationToken);
                        var msg = string.Format(mainYearWinnerSingle, yearWinnerUser.UserName);

                        await SendPicture(chatId, yearWinner, msg, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(500, cancellationToken);
                        var mainYearWinnerMultiple = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.MainYearWinnerMultiple, cancellationToken);

                        if (!string.IsNullOrEmpty(mainYearWinnerMultiple))
                        {
                            var anotherWinnersNames = users
                                .Where(u => anotherWinners.Select(p => p.UserId).Contains(u.UserId))
                                .Select(u => u.UserName)
                                .Distinct()
                                .ToArray();
                            
                            var msg = string.Format(mainYearWinnerMultiple, string.Join(", ", anotherWinnersNames));
                            await botClient.TrySendTextMessageAsync(chatId, msg, logger: logger, cancellationToken: cancellationToken);
                        }

                        foreach (var player in anotherWinners.DistinctBy(w => w.UserId))
                        {
                            await SendPicture(chatId, player, player.UserName, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in {Name} for {ChatId}", nameof(YearlyWinnerJobHandler), chatId);
        }
    }

    private async Task SendPicture(long chatId, PlayerCountViewModel player, string msg, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImageStream = await httpClient.GetStreamAsync("GoldenCup.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);

        logger.LogInformation("{Name} Downloaded GoldenCup.png image for {ChatId}", nameof(YearlyWinnerJobHandler), chatId);

        UserProfilePhotos? userProfilePhotos = null;
        try
        {
            logger.LogInformation("{Name} for Chat: {ChatId}, User: {UserId} Loading user photos", nameof(YearlyWinnerJobHandler), chatId, player.UserId);
            userProfilePhotos = await botClient.GetUserProfilePhotos(player.UserId, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in {Name} for {ChatId}, User: {UserId}, loading user photos", nameof(YearlyWinnerJobHandler), chatId, player.UserId);
        }

        if (userProfilePhotos == null || userProfilePhotos.Photos.Length == 0)
        {
            logger.LogInformation("{Name} for {ChatId}, photos not loaded", nameof(YearlyWinnerJobHandler), chatId);
            await botClient.TrySendPhotoAsync(
                logger: logger,
                chatId: chatId,
                photo: InputFile.FromStream(bowlImageStream),
                caption: msg,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
        else
        {
            logger.LogInformation("{Name} for chat: {ChatId}, user {UserId} photo loaded", nameof(YearlyWinnerJobHandler), chatId, player.UserId);

            var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
            var photoFile = await botClient.GetFile(photoSize?.FileId ?? string.Empty, cancellationToken);

            logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, photo file info loaded", nameof(YearlyWinnerJobHandler), chatId, player.UserId);

            var avatarStream = new MemoryStream();
            await botClient.DownloadFile(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);

            logger.LogInformation("{Name} chat: {ChatId}, user {UserId} photo file downloaded", nameof(YearlyWinnerJobHandler), chatId, player.UserId);

            try
            {
                var winnerImage = await ImageProcessor.GetWinnerImageYear(bowlImageStream, avatarStream, DateTime.Now.Year);

                logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, winner image created. Sending", nameof(YearlyWinnerJobHandler), chatId, player.UserId);
                await botClient.TrySendPhotoAsync(
                    logger: logger,
                    chatId: chatId,
                    photo: winnerImage,
                    caption: msg,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception in {Name} for char {ChatId}, user {UserId}", nameof(YearlyWinnerJobHandler), chatId, player.UserId);
                await botClient.TrySendPhotoAsync(
                    logger: logger,
                    chatId: chatId,
                    photo: InputFile.FromStream(bowlImageStream),
                    caption: msg,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<string> GetMonthName(int number, CancellationToken cancellationToken)
    {
        var messageName = number switch
        {
            1 => TextMessageService.TextMessageNames.Month1,
            2 => TextMessageService.TextMessageNames.Month2,
            3 => TextMessageService.TextMessageNames.Month3,
            4 => TextMessageService.TextMessageNames.Month4,
            5 => TextMessageService.TextMessageNames.Month5,
            6 => TextMessageService.TextMessageNames.Month6,
            7 => TextMessageService.TextMessageNames.Month7,
            8 => TextMessageService.TextMessageNames.Month8,
            9 => TextMessageService.TextMessageNames.Month9,
            10 => TextMessageService.TextMessageNames.Month10,
            11 => TextMessageService.TextMessageNames.Month11,
            12 => TextMessageService.TextMessageNames.Month12,
            _ => string.Empty
        };

        return await messageService.GetMessageByNameAsync(messageName, cancellationToken);
    }
}