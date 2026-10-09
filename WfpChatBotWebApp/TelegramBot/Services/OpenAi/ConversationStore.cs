using Microsoft.Extensions.Caching.Memory;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IConversationStore
{
    bool TryGetConversationId(string key, out string conversationId);
    bool ContainsKey(string key);
    void SetConversationId(string key, string conversationId);
    void Remove(string key);
}

public class ConversationStore(IMemoryCache memoryCache) : IConversationStore
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromDays(7);

    public bool TryGetConversationId(string key, out string conversationId)
    {
        if (memoryCache.TryGetValue<string>(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            conversationId = value;
            return true;
        }

        conversationId = string.Empty;
        return false;
    }

    public bool ContainsKey(string key) => memoryCache.TryGetValue<string>(key, out _);

    public void SetConversationId(string key, string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        memoryCache.Set(key, conversationId, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });
    }

    public void Remove(string key) => memoryCache.Remove(key);
}
