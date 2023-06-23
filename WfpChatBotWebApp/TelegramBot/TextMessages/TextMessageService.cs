using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.TelegramBot.TextMessages;

public interface ITextMessageService
{
    Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken);
}

public class TextMessageService : ITextMessageService
{
    private readonly AppDbContext _appDbContext;
    private readonly IMemoryCache _cache;

    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public TextMessageService(AppDbContext appDbContext, IMemoryCache cache)
    {
        _appDbContext = appDbContext;
        _cache = cache;
    }

    public async Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        if (_cache.TryGetValue<string>(name, out var text))
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text;
        }

        try
        {
            await Semaphore.WaitAsync(cancellationToken);

            if (_cache.TryGetValue(name, out text))
            {
                return string.IsNullOrEmpty(text) ? string.Empty : text;
            }
            else
            {
                var data = await _appDbContext.TextMessages.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);

                if (data != null && !string.IsNullOrEmpty(data.Text))
                {
                    _cache.Set(name, data.Text, TimeSpan.FromDays(1));
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

