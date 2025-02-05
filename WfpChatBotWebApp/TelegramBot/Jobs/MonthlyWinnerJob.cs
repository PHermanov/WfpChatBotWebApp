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
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessMonthlyWinnerForChat(allChatIds[i], cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private async Task ProcessMonthlyWinnerForChat(long chatId, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} for {ChatId}", nameof(MonthlyWinnerJobHandler), chatId);

        var httpClient = httpClientFactory.CreateClient("Pictures");
        var bowlImageStream = await httpClient.GetStreamAsync("bowl.png" + configuration.GetValue<string>("StickerSas"), cancellationToken);
        
        logger.LogInformation("{Name} Downloaded bowl image for {ChatId}", nameof(MonthlyWinnerJobHandler), chatId);
        
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
                    logger.LogInformation("{Name} for Chat: {ChatId}, User: {UserId} Loading user photos", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId);
                    userProfilePhotos = await botClient.GetUserProfilePhotos(monthWinner.UserId, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError("{Name} for {ChatId}, User: {UserId}, Loading user photos. Exception: {exception}", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId, e.Message);
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
                    logger.LogInformation("{Name} for chat: {ChatId}, user {UserId} photo loaded", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId);

                    var photoSize = userProfilePhotos.Photos[0].MaxBy(p => p.Height);
                    var photoFile = await botClient.GetFile(photoSize?.FileId ?? string.Empty, cancellationToken);

                    logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, photo file info loaded", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId);

                    var avatarStream = new MemoryStream();
                    await botClient.DownloadFile(photoFile.FilePath ?? string.Empty, avatarStream, cancellationToken);

                    logger.LogInformation("{Name} chat: {ChatId}, user {UserId} photo file downloaded", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId);

                    try
                    {
                        var winnerImage = await ImageProcessor.GetWinnerImageMonth(bowlImageStream, avatarStream, DateTime.Today);
                        
                        logger.LogInformation("{Name} chat: {ChatId}, user {UserId}, winner image created. Sending", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId);

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
                        logger.LogError("{Name} for char {ChatId}, user {UserId} Exception in GetWinnerImageMonth {e}", nameof(MonthlyWinnerJobHandler), chatId, monthWinner.UserId, e.Message);

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
                logger.LogError("{Name} for {ChatId}, Winner not selected", nameof(MonthlyWinnerJobHandler), chatId);
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name} for {ChatId}, Exception: {e}", nameof(MonthlyWinnerJobHandler), chatId, e);
        }
    }
}