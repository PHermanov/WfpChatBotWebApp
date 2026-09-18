using MediatR;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class MonthlyWinnerJobRequest : IRequest;

public class MonthlyWinnerJobHandler(
    ITextMessageService textMessageService,
    IGameRepository repository,
    IWinnerAnnouncementService announcements,
    ILogger<MonthlyWinnerJobRequest> logger)
    : IRequestHandler<MonthlyWinnerJobRequest>
{
    public async Task Handle(MonthlyWinnerJobRequest request, CancellationToken cancellationToken)
    {
        var allChatIds = await repository.GetGameEnabledChatIdsAsync(cancellationToken);
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

                await announcements.SendAsync(chatId, monthWinner.UserId, monthWinner.UserName, DateTime.Today,
                    WinnerPeriod.Month, message, ParseMode.Markdown, cancellationToken);
            }
            else
            {
                logger.LogError("MonthlyWinnerJobHandler for {ChatId}, Winner not selected", chatId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger.LogError(e, "MonthlyWinnerJobHandler for {ChatId}, Exception", chatId);
        }
    }
}