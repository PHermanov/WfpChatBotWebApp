namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IContextKeysService
{
    bool TryGetValue(string key, out Guid contextKey);
    Guid GetValue(string key);
    Guid SetValue(string key, Guid value);
    void RemoveValue(string key);
}

public class ContextKeysService : IContextKeysService
{
    private readonly Dictionary<string, Guid> _contextKeys = new();

    public bool TryGetValue(string key, out Guid contextKey)
        => _contextKeys.TryGetValue(key, out contextKey);

    public Guid GetValue(string key) => _contextKeys[key];

    public Guid SetValue(string key, Guid value) => _contextKeys[key] = value;

    public void RemoveValue(string key) => _contextKeys.Remove(key);
}