using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IWinnerAnnouncementService
{
    Task SendAsync(long chatId, long userId, string userName, DateTime date, WinnerPeriod period,
        string caption, ParseMode parseMode, CancellationToken cancellationToken, int replyToMessageId = 0);
}

public class WinnerAnnouncementService(
    ITelegramBotClient botClient,
    IWinnerArtworkService artwork,
    ILogger<WinnerAnnouncementService> logger) : IWinnerAnnouncementService
{
    public async Task SendAsync(long chatId, long userId, string userName, DateTime date, WinnerPeriod period,
        string caption, ParseMode parseMode, CancellationToken cancellationToken, int replyToMessageId = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        caption = $"{caption}{Environment.NewLine}{date.ToString(period == WinnerPeriod.Month ? "MMMM yyyy" : "yyyy", CultureInfo.InvariantCulture)}";
        try
        {
            var avatar = await botClient.TryGetUserProfilePhoto(userId, logger, cancellationToken);
            var bytes = await artwork.CreateAsync(userName, date, period, avatar, cancellationToken);
            if (bytes is { Length: > 0 })
            {
                using var stream = new MemoryStream(bytes);
                var sent = await botClient.TrySendPhotoAsync(logger, chatId, InputFile.FromStream(stream, "winner.png"),
                    parseMode, caption, replyToMessageId: replyToMessageId, cancellationToken: cancellationToken);
                if (sent is not null) return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            logger.LogWarning("Winner artwork failed in chat {ChatId} for user {UserId}: {ErrorType}: {Error}", chatId, userId, e.GetType().Name, e.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Sending text-only winner announcement in chat {ChatId} for user {UserId}", chatId, userId);
        await botClient.TrySendTextMessageAsync(chatId, caption, logger, parseMode,
            replyToMessageId: replyToMessageId, cancellationToken: cancellationToken);
    }
}
