using System.Collections.Concurrent;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // Responses APIs are experimental in OpenAI 2.9.1.

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

public class OpenAiChatMessageQueue
{
    private readonly ConcurrentQueue<ResponseItem> _internalQueue = new();
    private readonly Lock _lockObject = new();

    public void Enqueue(ResponseItem obj) => EnqueueRange([obj]);

    public void EnqueueRange(IEnumerable<ResponseItem> items)
    {
        lock (_lockObject)
        {
            foreach (var item in items)
            {
                _internalQueue.Enqueue(item);
            }
        }
    }

    public ResponseItem[] ToArray()
    {
        lock (_lockObject)
        {
            return _internalQueue.ToArray();
        }
    }
}
