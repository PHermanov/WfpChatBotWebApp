using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using User = WfpChatBotWebApp.Persistence.Entities.User;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class DailyWinnerJobRequest : IRequest;

public class DailyWinnerJobHandler(ITelegramBotClient botClient,  
    ITextMessageService textMessageService, 
    IGameRepository repository, 
    ISoraService soraService,
    IRandomService randomService,
    ILogger<DailyWinnerJobHandler> logger) : IRequestHandler<DailyWinnerJobRequest>
{
    public async Task Handle(DailyWinnerJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed {Name} at {Now}", nameof(DailyWinnerJobHandler), DateTime.UtcNow);
        
        var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);

        logger.LogInformation("Got {Chats} chats", string.Join(',', allChatIds));
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessDailyWinnerForChat(allChatIds[i], cancellationToken);
        }
    }

    private async Task ProcessDailyWinnerForChat(long chatId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed {Name} for {ChatId}", nameof(ProcessDailyWinnerForChat), chatId);

        try
        {
            var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);
            
            await ProcessMissedGames(chatId, users, cancellationToken);
            
            var todayResult = await repository.GetTodayResultAsync(chatId, cancellationToken);

            if (todayResult != null)
            {
                await SendTodayWinnerMessage(chatId, todayResult, cancellationToken);
                return;
            }
            
            var newWinner = await SelectWinnerForDate(chatId, DateTime.Today, users, cancellationToken);

            await SendNewWinnerMessage(chatId, newWinner, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in {ClassName}", nameof(DailyWinnerJobHandler));
        }
    }

    private async Task SendNewWinnerMessage(long chatId, User newWinner, CancellationToken cancellationToken)
    {
        var messageTemplateNew = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.NewWinner, cancellationToken);
        var caption = string.Format(messageTemplateNew, newWinner.GetUserMention());
        var prompt = string.Format(messageTemplateNew, newWinner.UserName);
        
        var stream = await soraService.GetVideo(prompt, 5, cancellationToken);

        if (stream != null)
        {
            await botClient.TrySendAnimationAsync(
                chatId: chatId,
                video: InputFile.FromStream(stream, "file.mp4"),
                caption: caption,
                logger: logger,
                cancellationToken: cancellationToken);
        }
    }

    private async Task SendTodayWinnerMessage(long chatId, Result todayResult, CancellationToken cancellationToken)
    {
        var todayWinner = await repository.GetUserByUserIdAndChatIdAsync(todayResult.ChatId, todayResult.UserId, cancellationToken);
        if (todayWinner == null)
            return;
                
        var messageTemplateAlreadySet = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.TodayWinnerAlreadySet, cancellationToken);     
                
        await botClient.TrySendTextMessageAsync(
            chatId: chatId,
            text: string.Format(messageTemplateAlreadySet, todayWinner.GetUserMention()),
            parseMode: ParseMode.Markdown,
            logger: logger,
            cancellationToken: cancellationToken);
    }

    private async Task ProcessMissedGames(long chatId, User[] users, CancellationToken cancellationToken)
    {
        var lastGame = await repository.GetLastPlayedGameAsync(chatId, cancellationToken);

        if (lastGame != null)
        {
            var date = lastGame.PlayDate.AddDays(1);
            var results = new List<string>();

            while (date.Date < DateTime.Today)
            {
                var winner = await SelectWinnerForDate(chatId, date, users, cancellationToken);
                
                results.Add($"*{date:dd.MM.yyyy}* - {winner.GetUserMention()}");

                date = date.AddDays(1);
            }

            if (results.Count != 0)
            {
                var template = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.MissedGames, cancellationToken);

                await botClient.TrySendTextMessageAsync(
                    chatId: chatId,
                    text: string.Format(template, $"{Environment.NewLine}{string.Join(Environment.NewLine, results)}"),
                    parseMode: ParseMode.Markdown,
                    logger: logger,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<User> SelectWinnerForDate(long chatId, DateTime date, User[] users, CancellationToken cancellationToken)
    {
        var random = await randomService.GetRandomNumber(users.Length);
        var newWinner = users[random];

        var dayResult = new Result
        {
            ChatId = chatId,
            UserId = newWinner.UserId,
            PlayDate = date
        };

        await repository.SaveResultAsync(dayResult, cancellationToken);

        return newWinner;
    }
}