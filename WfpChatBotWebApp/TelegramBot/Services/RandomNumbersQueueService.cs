namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IRandomNumbersQueueService
{
    bool CanPeek(int max);
    int GetNextRandomNumber(int max);
    void EnqueueRange(int max, int[] values);
}

public class RandomNumbersQueueService : IRandomNumbersQueueService
{
    private readonly Lock _lockObject = new();
    private Dictionary<int, Queue<int>> RandomNumbers { get; } = new();

    public bool CanPeek(int max) => RandomNumbers.TryGetValue(max, out var ints) && ints.Count > 0;

    public int GetNextRandomNumber(int max)
    {
        lock (_lockObject)
        {
            if (RandomNumbers.TryGetValue(max, out var ints))
                return ints.Dequeue();
        }
        return -1;
    }

    public void EnqueueRange(int max, int[] values)
    {
       if (RandomNumbers.TryGetValue(max, out var ints))
           lock (_lockObject)
           {
               for (var i = 0; i < values.Length; i++)
                   ints.Enqueue(values[i]);
           }
       else
           lock (_lockObject)
           {
               RandomNumbers.Add(max, new Queue<int>(values));
           }
    }
}