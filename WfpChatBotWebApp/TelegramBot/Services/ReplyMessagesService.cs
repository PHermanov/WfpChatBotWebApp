using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IReplyMessagesService
{
    Task<string> GetMessageByKeyAsync(string name, CancellationToken cancellationToken);
}

public class ReplyMessagesService(AppDbContext appDbContext, IMemoryCache cache) : IReplyMessagesService
{
    private const string CacheKey = nameof(CacheKey);
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<string> GetMessageByKeyAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (cache.TryGetValue(CacheKey, out Dictionary<string, string>? values) && values != null)
            return values.TryGetValue(key, out var val) ? val : string.Empty;

        try
        {
            await Semaphore.WaitAsync(cancellationToken);

            if (cache.TryGetValue(CacheKey, out values) && values != null)
            {
                return values.TryGetValue(key, out var val) ? val : string.Empty;
            }
            else
            {
                var allData = await appDbContext.ReplyMessages.ToArrayAsync(cancellationToken);

                if (allData.Length > 0)
                {
                    values = allData.ToDictionary(r => r.MessageKey, r => r.MessageValue);

                    cache.Set(CacheKey, values, TimeSpan.FromDays(1));

                    return values.TryGetValue(key, out var val) ? val : string.Empty;
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }
}