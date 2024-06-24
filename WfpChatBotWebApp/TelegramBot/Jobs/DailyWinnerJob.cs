using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.Persistence.Entities;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class DailyWinnerJobRequest : IRequest
{
}

public class DailyWinnerJobHandler(ITelegramBotClient botClient, IGameRepository gameRepository, ILogger<DailyWinnerJobHandler> logger)
    : IRequestHandler<DailyWinnerJobRequest>
{
    public async Task Handle(DailyWinnerJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed {Name} at {Now}", nameof(DailyWinnerJobHandler), DateTime.UtcNow);
        
        var allChatIds = await gameRepository.GetAllChatsIdsAsync();

        logger.LogInformation("Got {Chats} chats", string.Join(',', allChatIds));
        
        for (var i = 0; i < allChatIds.Length; i++)
        {
            await ProcessDailyWinnerForChat(allChatIds[i]);
        }
    }

    private async Task ProcessDailyWinnerForChat(long chatId)
    {
        logger.LogInformation("Executed {Name} for {ChatId}", nameof(ProcessDailyWinnerForChat), chatId);
        
        try
        {
            var todayResult = await gameRepository.GetTodayResultAsync(chatId);

            if (todayResult != null)
            {
                // Messages.TodayWinnerAlreadySet;
                await botClient.TrySendTextMessageAsync(
                    chatId: chatId,
                    text: "already played",
                    parseMode: ParseMode.Markdown);
            }

            var users = await gameRepository.GetActiveUsersAsync(chatId);
            var newWinner = users[new Random().Next(users.Count)];

            todayResult = new Result
            {
                ChatId = chatId,
                UserId = newWinner.UserId,
                PlayedAt = DateTime.Today
            };

            await gameRepository.SaveResultAsync(todayResult);

            var messageTemplate = "New Winner {0}"; // Messages.NewWinner;
            var msg = string.Format(messageTemplate, newWinner.GetUserMention());

            await botClient.TrySendTextMessageAsync(
                chatId: chatId,
                text: msg,
                parseMode: ParseMode.Markdown);

            // await botClient.TrySendStickerAsync(
            //     chatId: chatId,
            //     sticker: InputFile.FromUri(StickersSelector.SelectRandomFromSet(StickersSelector.StickerSet.Yoba)));
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }
    }
}