using Microsoft.Extensions.Caching.Memory;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IConversationStore
{
    bool TryGetConversationId(string key, out string conversationId);
    bool ContainsKey(string key);
    void SetConversationId(string key, string conversationId);
    void Remove(string key);
    bool TryGetLastImageFileId(string conversationId, out string fileId);
    void SetLastImageFileId(string conversationId, string fileId);
}

public class ConversationStore(IMemoryCache memoryCache) : IConversationStore
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromDays(7);
    private const string LastImageKeyPrefix = "last_image:";

    public bool TryGetConversationId(string key, out string conversationId) => TryGetValue(key, out conversationId);

    public bool ContainsKey(string key) => memoryCache.TryGetValue<string>(key, out _);

    public void SetConversationId(string key, string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        SetValue(key, conversationId);
    }

    public void Remove(string key) => memoryCache.Remove(key);

    public bool TryGetLastImageFileId(string conversationId, out string fileId) =>
        TryGetValue(LastImageKeyPrefix + conversationId, out fileId);

    public void SetLastImageFileId(string conversationId, string fileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        SetValue(LastImageKeyPrefix + conversationId, fileId);
    }

    private bool TryGetValue(string key, out string value)
    {
        if (memoryCache.TryGetValue<string>(key, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            value = cached;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private void SetValue(string key, string value) =>
        memoryCache.Set(key, value, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });
}
