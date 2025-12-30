using System.Collections.Concurrent;
using OpenAI.Chat;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class OpenAiChatMessageQueue
{
    private readonly ConcurrentQueue<ChatMessage> _internalQueue = new();
    private readonly Lock _lockObject = new();

    public void Enqueue(ChatMessage obj)
    {
        lock (_lockObject)
        {
            _internalQueue.Enqueue(obj);
        }
    }
    
    public ChatMessage[] ToArray()
    {
        lock (_lockObject)
        {
            return _internalQueue.ToArray();
        }
    }
}