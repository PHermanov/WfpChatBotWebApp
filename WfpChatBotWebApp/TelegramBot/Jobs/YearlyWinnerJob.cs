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
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

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
        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
        logger.LogInformation("YearlyWinnerJobHandler for {Chats} at {Now}", string.Join(',', allChatIds), DateTime.UtcNow);

        if (allChatIds.Length == 0)
            return;

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
            logger.LogInformation("YearlyWinnerJobHandler for {ChatId}", chatId);

            var year = DateTime.Today.Year;
            var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);

            // welcome message
            var yearSummarizeMsg = await messageService.GetMessageByNameAsync(Messages.YearSummarizeMsg, cancellationToken);
            if (!string.IsNullOrEmpty(yearSummarizeMsg))
            {
                logger.LogInformation("YearlyWinnerJobHandler for {ChatId} sending welcome message", chatId);
                await botClient.TrySendTextMessageAsync(
                    chatId: chatId,
                    text: yearSummarizeMsg,
                    parseMode: ParseMode.Markdown,
                    logger: logger,
                    cancellationToken: cancellationToken);
            }

            // year winner by total count
            var yearWinnerByCount = await repository.GetYearWinnerByCountAsync(chatId, year, cancellationToken);
            var yearWinnerByCountMessageTemplate = await messageService.GetMessageByNameAsync(Messages.YearByCountWinnerMsg, cancellationToken);

            if (yearWinnerByCount != null && !string.IsNullOrEmpty(yearWinnerByCountMessageTemplate))
            {
                logger.LogInformation("YearlyWinnerJobHandler for {ChatId} YearWinnerByCount is {Winner}", chatId, yearWinnerByCount.UserName);

                var user = users.FirstOrDefault(u => u.UserId == yearWinnerByCount.UserId);
                if (user != null)
                {
                    if (user.Inactive)
                    {
                        var userMissing = await messageService.GetMessageByNameAsync(Messages.UserMissing, cancellationToken);
                        yearWinnerByCount.UserName += userMissing;
                    }

                    var yearWinnerByCountMessage = string.Format(yearWinnerByCountMessageTemplate, user.UserName);
                    await botClient.TrySendTextMessageAsync(chatId, yearWinnerByCountMessage, logger: logger, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }
                else
                {
                    logger.LogError("YearlyWinnerJobHandler for {ChatId} YearWinnerByCount: user is null", chatId);
                }
            }

            await Task.Delay(500, cancellationToken);

            // year winner by months count

            // find winner of every month
            logger.LogInformation("YearlyWinnerJobHandler for {ChatId} find winner of every month", chatId);
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
                    logger.LogInformation("YearlyWinnerJobHandler for {ChatId} YearWinner is {Winner}", chatId, yearWinnerUser.UserName);

                    // display winners of all months
                    var monthStrRes = new StringBuilder();
                    foreach (var (monthNumber, winner) in monthWinners.OrderBy(k => k.Key))
                    {
                        var monthName = await GetMonthName(monthNumber, cancellationToken);
                        var monthDisplay = string.IsNullOrEmpty(monthName) ? monthNumber.ToString() : monthName;
                        var monthWinnerUserName = users.FirstOrDefault(u => u.UserId == winner.UserId)?.UserName;
                        monthStrRes.AppendLine($"<i>{monthDisplay}</i>: <b>{monthWinnerUserName}</b>");
                    }

                    var yearAllMonthWinnersMsg = await messageService.GetMessageByNameAsync(Messages.YearAllMonthWinnersMsg, cancellationToken);
                    if (!string.IsNullOrEmpty(yearAllMonthWinnersMsg) && monthStrRes.Length > 0)
                    {
                        await Task.Delay(500, cancellationToken);
                        await botClient.TrySendTextMessageAsync(chatId, yearAllMonthWinnersMsg, logger: logger, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                        await Task.Delay(500, cancellationToken);
                        await botClient.TrySendTextMessageAsync(chatId, monthStrRes.ToString(), logger: logger, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                    }

                    // check if there are more players with the same win count as leader
                    var anotherWinners = monthWinners.Values
                        .Where(w => w.Count == yearWinner.Count)
                        .Distinct()
                        .ToList();

                    if (anotherWinners.Count == 1)
                    {
                        await Task.Delay(500, cancellationToken);
                        var mainYearWinnerSingle = await messageService.GetMessageByNameAsync(Messages.MainYearWinnerSingle, cancellationToken);
                        var msg = string.Format(mainYearWinnerSingle, yearWinnerUser.UserName);

                        await SendPicture(chatId, yearWinner, msg, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(500, cancellationToken);
                        var mainYearWinnerMultiple = await messageService.GetMessageByNameAsync(Messages.MainYearWinnerMultiple, cancellationToken);

                        if (!string.IsNullOrEmpty(mainYearWinnerMultiple))
                        {
                            var anotherWinnersNames = users
                                .Where(u => anotherWinners.Select(p => p.UserId).Contains(u.UserId))
                                .Select(u => u.UserName)
                                .Distinct()
                                .ToArray();
                            
                            var msg = string.Format(mainYearWinnerMultiple, string.Join(", ", anotherWinnersNames));
                            await botClient.TrySendTextMessageAsync(chatId, msg, logger: logger, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
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
            logger.LogError(e, "Exception in YearlyWinnerJobHandler for {ChatId}", chatId);
        }
    }

    private async Task SendPicture(long chatId, PlayerCountViewModel player, string msg, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImageStream = await httpClient.GetStreamAsync("GoldenCup.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);

        logger.LogInformation("YearlyWinnerJobHandler Downloaded GoldenCup.png image for {ChatId}", chatId);

        UserProfilePhotos? userProfilePhotos = null;
        try
        {
            logger.LogInformation("YearlyWinnerJobHandler for Chat: {ChatId}, User: {UserId} Loading user photos", chatId, player.UserId);
            userProfilePhotos = await botClient.GetUserProfilePhotos(player.UserId, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in YearlyWinnerJobHandler for {ChatId}, User: {UserId}, loading user photos", chatId, player.UserId);
        }

        if (userProfilePhotos == null || userProfilePhotos.Photos.Length == 0)
        {
            logger.LogInformation("YearlyWinnerJobHandler for {ChatId}, photos not loaded", chatId);
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
            logger.LogInformation("YearlyWinnerJobHandler for {ChatId} : user {UserId} photo loaded", chatId, player.UserId);

            var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
            var photoFile = await botClient.GetFile(photoSize?.FileId ?? string.Empty, cancellationToken);

            logger.LogInformation("YearlyWinnerJobHandler for {ChatId} : user {UserId}, photo file info loaded", chatId, player.UserId);

            var avatarStream = new MemoryStream();
            await botClient.DownloadFile(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);

            logger.LogInformation("YearlyWinnerJobHandler for {ChatId} : user {UserId} photo file downloaded", chatId, player.UserId);

            try
            {
                var winnerImage = await ImageProcessor.GetWinnerImageYear(bowlImageStream, avatarStream, DateTime.Now.Year);

                logger.LogInformation("YearlyWinnerJobHandler for {ChatId} : user {UserId}, winner image created. Sending", chatId, player.UserId);
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
                logger.LogError(e, "Exception in YearlyWinnerJobHandler for {ChatId} : user {UserId}", chatId, player.UserId);
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
            1 => Messages.Month1,
            2 => Messages.Month2,
            3 => Messages.Month3,
            4 => Messages.Month4,
            5 => Messages.Month5,
            6 => Messages.Month6,
            7 => Messages.Month7,
            8 => Messages.Month8,
            9 => Messages.Month9,
            10 => Messages.Month10,
            11 => Messages.Month11,
            12 => Messages.Month12,
            _ => string.Empty
        };

        return await messageService.GetMessageByNameAsync(messageName, cancellationToken);
    }
}