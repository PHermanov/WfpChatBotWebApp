using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

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
        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
        logger.LogInformation("MonthlyWinnerJobHandler for {Chats} at {Now}", string.Join(',', allChatIds), DateTime.UtcNow);

        if (allChatIds.Length == 0)
            return;
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessMonthlyWinnerForChat(allChatIds[i], cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    private async Task ProcessMonthlyWinnerForChat(long chatId, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImageStream = await httpClient.GetStreamAsync("bowl.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);

        logger.LogInformation("MonthlyWinnerJobHandler for {ChatId} : Downloaded bowl image ", chatId);
        
        try
        {
            var monthWinner = await repository.GetWinnerForMonthAsync(chatId, DateTime.Now, cancellationToken);

            logger.LogInformation("MonthlyWinnerJobHandler for {ChatId} : Month winner {winner}", chatId, monthWinner?.UserId);

            if (monthWinner != null)
            {
                var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);
                var mention = users
                    .FirstOrDefault(u => u.UserId == monthWinner.UserId)!
                    .GetUserMention();

                var monthWinnerMessage = await textMessageService.GetMessageByNameAsync(Messages.MonthWinner, cancellationToken);
                var congratsMessage = await textMessageService.GetMessageByNameAsync(Messages.Congrats, cancellationToken);

                var message = $"{monthWinnerMessage}{Environment.NewLine}\u269C {mention} \u269C{Environment.NewLine}{congratsMessage}";

                UserProfilePhotos? userProfilePhotos = null;
                try
                {
                    logger.LogInformation("MonthlyWinnerJobHandler for {ChatId} : User: {UserId}, Loading user photos", chatId, monthWinner.UserId);
                    userProfilePhotos = await botClient.GetUserProfilePhotos(monthWinner.UserId, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "MonthlyWinnerJobHandler for {ChatId} : User: {UserId}, Loading user photos. Exception", chatId, monthWinner.UserId);
                }

                if (userProfilePhotos == null || userProfilePhotos.Photos.Length == 0)
                {
                    logger.LogInformation("MonthlyWinnerJobHandler for {ChatId} : photos not loaded", chatId);

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
                    logger.LogInformation("MonthlyWinnerJobHandler for {ChatId} : User {UserId} photo loaded", chatId, monthWinner.UserId);

                    var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
                    var photoFile = await botClient.GetFile(photoSize?.FileId ?? string.Empty, cancellationToken);

                    logger.LogInformation("MonthlyWinnerJobHandler chat: {ChatId}, User {UserId}, photo file info loaded", chatId, monthWinner.UserId);

                    var avatarStream = new MemoryStream();
                    await botClient.DownloadFile(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);

                    logger.LogInformation("MonthlyWinnerJobHandler chat: {ChatId}, User {UserId} photo file downloaded", chatId, monthWinner.UserId);

                    try
                    {
                        var winnerImage = await ImageProcessor.GetWinnerImageMonth(bowlImageStream, avatarStream, DateTime.Today);

                        logger.LogInformation("MonthlyWinnerJobHandler for {ChatId} : User {UserId}, winner image created. Sending", chatId, monthWinner.UserId);

                        await botClient.TrySendPhotoAsync(
                            logger: logger,
                            chatId: chatId,
                            photo: winnerImage,
                            caption: message,
                            parseMode: ParseMode.Markdown,
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "MonthlyWinnerJobHandler for {ChatId} : User {UserId} Exception in GetWinnerImageMonth", chatId, monthWinner.UserId);

                        await botClient.TrySendPhotoAsync(
                            logger: logger,
                            chatId: chatId,
                            photo: InputFile.FromStream(bowlImageStream),
                            caption: message,
                            parseMode: ParseMode.Markdown,
                            cancellationToken: cancellationToken);
                    }
                }
            }
            else
            {
                logger.LogError("MonthlyWinnerJobHandler for {ChatId}, Winner not selected", chatId);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "MonthlyWinnerJobHandler for {ChatId}, Exception", chatId);
        }
    }
}