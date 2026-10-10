using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public interface IConversationStore
{
    bool TryGetConversationId(string key, out string conversationId);
    bool ContainsKey(string key);
    void SetConversationId(string key, string conversationId);
    void Remove(string key);
    bool TryGetLastImage(string conversationId, [NotNullWhen(true)] out ConversationImage? image);
    void SetLastImage(string conversationId, ConversationImage image);
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

    public bool TryGetLastImage(string conversationId, [NotNullWhen(true)] out ConversationImage? image) =>
        memoryCache.TryGetValue(LastImageKeyPrefix + conversationId, out image) && image is not null;

    public void SetLastImage(string conversationId, ConversationImage image)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(image.FileId);
        SetValue(LastImageKeyPrefix + conversationId, image);
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

    private void SetValue(string key, object value) =>
        memoryCache.Set(key, value, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SlidingExpiration
        });
}
