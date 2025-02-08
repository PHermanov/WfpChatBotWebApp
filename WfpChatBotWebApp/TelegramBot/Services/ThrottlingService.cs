using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IThrottlingService
{
    Task<bool> IsAllowed(Message message, string commandKey, CancellationToken cancellationToken);
}

public class ThrottlingService(
    IMemoryCache memoryCache,
    ITelegramBotClient botClient,
    IGameRepository gameRepository,
    ITextMessageService textMessageService,
    ILogger<ThrottlingService> logger) : IThrottlingService
{
    public async Task<bool> IsAllowed(Message message, string commandKey, CancellationToken cancellationToken)
    {
        var cacheKey = $"{message.Chat.Id}_{message.From!.Id}_{commandKey}";

        if (memoryCache.TryGetValue(cacheKey, out bool showMessage))
        {
            try
            {
                await botClient.DeleteMessage(message.Chat.Id, message.MessageId, cancellationToken);

                if (showMessage)
                {
                    var user = await gameRepository.GetUserByUserIdAndChatIdAsync(message.Chat.Id, message.From.Id, cancellationToken);
                    if (user == null)
                        return false;

                    var text = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.TakeRest, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await botClient.TrySendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: string.Format(text, user.GetUserMention()),
                            parseMode: ParseMode.Markdown,
                            logger: logger,
                            cancellationToken: cancellationToken);
                    }

                    memoryCache.Set(cacheKey, false, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(2));
                }

                return false;
            }
            catch
            {
                logger.LogError("Can not delete message");
                return false;
            }
        }

        memoryCache.Set(cacheKey, true, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(2));
        return true;
    }
}