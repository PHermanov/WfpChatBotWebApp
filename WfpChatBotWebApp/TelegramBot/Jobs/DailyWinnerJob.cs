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
    IGameRepository gameRepository, 
    IStickerService stickerService, 
    ILogger<DailyWinnerJobHandler> logger)
    : IRequestHandler<DailyWinnerJobRequest>
{
    public async Task Handle(DailyWinnerJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executed {Name} at {Now}", nameof(DailyWinnerJobHandler), DateTime.UtcNow);
        
        var allChatIds = await gameRepository.GetAllChatsIdsAsync(cancellationToken);

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
            var todayResult = await gameRepository.GetTodayResultAsync(chatId, cancellationToken);

            if (todayResult != null)
            {
                await botClient.TrySendTextMessageAsync(
                    chatId: chatId,
                    text: await textMessageService.GetMessageByNameAsync(TextMessageNames.TodayWinnerAlreadySet, cancellationToken),
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
                return;
            }

            var users = await gameRepository.GetActiveUsersAsync(chatId, cancellationToken);
            var newWinner = users[new Random().Next(users.Length)];

            todayResult = new Result
            {
                ChatId = chatId,
                UserId = newWinner.UserId,
                PlayedAt = DateTime.Today
            };

            await gameRepository.SaveResultAsync(todayResult, cancellationToken);

            var messageTemplate = await textMessageService.GetMessageByNameAsync(TextMessageNames.NewWinner, cancellationToken);
            
            await botClient.TrySendTextMessageAsync(
                chatId: chatId,
                text: string.Format(messageTemplate, newWinner.GetUserMention()),
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
        catch (Exception e)
        {
            logger.LogError("Exception in {ClassName}: {Message}", nameof(DailyWinnerJobHandler), e.Message);
        }
    }
}