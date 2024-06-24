using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.TelegramBot.TextMessages;

public interface ITextMessageService
{
    Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken);
}

public class TextMessageService(AppDbContext appDbContext, IMemoryCache cache) : ITextMessageService
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        if (cache.TryGetValue<string>(name, out var text))
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text;
        }

        try
        {
            await Semaphore.WaitAsync(cancellationToken);

            if (cache.TryGetValue(name, out text))
            {
                return string.IsNullOrEmpty(text) ? string.Empty : text;
            }
            else
            {
                var data = await appDbContext.TextMessages.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);

                if (data != null && !string.IsNullOrEmpty(data.Text))
                {
                    cache.Set(name, data.Text, TimeSpan.FromDays(1));
                    return data.Text;
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

