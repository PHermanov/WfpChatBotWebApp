using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class DailyWinnerJobRequest : IRequest;

public class DailyWinnerJobHandler(ITelegramBotClient botClient,  
    ITextMessageService textMessageService, 
    IGameRepository repository, 
    IStickerService stickerService, 
    ILogger<DailyWinnerJobHandler> logger)
    : IRequestHandler<DailyWinnerJobRequest>
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
            var users = await repository.GetActiveUsersAsync(chatId, cancellationToken);
            
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
            logger.LogError("Exception in {ClassName}: {Message}", nameof(DailyWinnerJobHandler), e.Message);
        }
    }

    private async Task SendNewWinnerMessage(long chatId, BotUser newWinner, CancellationToken cancellationToken)
    {
        var messageTemplateNew = await textMessageService.GetMessageByNameAsync(TextMessageNames.NewWinner, cancellationToken);
            
        await botClient.TrySendTextMessageAsync(
            chatId: chatId,
            text: string.Format(messageTemplateNew, newWinner.GetUserMention()),
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);

        var stickerUrl = await stickerService.GetRandomStickerFromSet(StickerService.StickerSet.Yoba, cancellationToken);
        if (!string.IsNullOrWhiteSpace(stickerUrl))
        {
            await botClient.TrySendStickerAsync(
                chatId: chatId,
                sticker: InputFile.FromUri(stickerUrl),
                cancellationToken: cancellationToken);
        }
    }

    private async Task SendTodayWinnerMessage(long chatId, Result todayResult, CancellationToken cancellationToken)
    {
        var todayWinner = await repository.GetUserByUserIdAndChatIdAsync(todayResult.ChatId, todayResult.UserId, cancellationToken);
        if (todayWinner == null)
            return;
                
        var messageTemplateAlreadySet = await textMessageService.GetMessageByNameAsync(TextMessageNames.TodayWinnerAlreadySet, cancellationToken);     
                
        await botClient.TrySendTextMessageAsync(
            chatId: chatId,
            text: string.Format(messageTemplateAlreadySet, todayWinner.GetUserMention()),
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    private async Task ProcessMissedGames(long chatId, BotUser[] users, CancellationToken cancellationToken)
    {
        var lastGame = await repository.GetLastPlayedGameAsync(chatId, cancellationToken);

        if (lastGame != null)
        {
            var date = lastGame.PlayedAt.AddDays(1);
            var results = new List<string>();

            while (date.Date < DateTime.Today)
            {
                var winner = await SelectWinnerForDate(chatId, date, users, cancellationToken);
                
                results.Add($"*{date:dd.MM.yyyy}* - {winner.GetUserMention()}");

                date = date.AddDays(1);
            }

            if (results.Count != 0)
            {
                var template = await textMessageService.GetMessageByNameAsync(TextMessageNames.MissedGames, cancellationToken);

                await botClient.TrySendTextMessageAsync(
                    chatId: chatId,
                    text: string.Format(template, $"{Environment.NewLine}{string.Join(Environment.NewLine, results)}"),
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<BotUser> SelectWinnerForDate(long chatId, DateTime date, BotUser[] users, CancellationToken cancellationToken)
    {
        var newWinner = users[new Random().Next(users.Length)];

        var dayResult = new Result
        {
            ChatId = chatId,
            UserId = newWinner.UserId,
            PlayedAt = date
        };

        await repository.SaveResultAsync(dayResult, cancellationToken);

        return newWinner;
    }
}