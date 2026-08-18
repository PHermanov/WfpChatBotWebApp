namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IRandomNumbersQueueService
{
    bool TryDequeue(int max, out int value);
    void EnqueueRange(int max, int[] values);
}

public class RandomNumbersQueueService : IRandomNumbersQueueService
{
    private readonly Lock _lockObject = new();
    private readonly Dictionary<int, Queue<int>> _randomNumbers = new();

    public bool TryDequeue(int max, out int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        lock (_lockObject)
        {
            if (_randomNumbers.TryGetValue(max, out var numbers) && numbers.TryDequeue(out value))
            {
                if (numbers.Count == 0)
                    _randomNumbers.Remove(max);

                return true;
            }
        }

        value = default;
        return false;
    }

    public void EnqueueRange(int max, int[] values)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        ArgumentNullException.ThrowIfNull(values);

        foreach (var value in values)
        {
            if (value < 0 || value >= max)
                throw new ArgumentOutOfRangeException(nameof(values), value, $"Values must be between 0 and {max - 1}.");
        }

        if (values.Length == 0)
            return;

        lock (_lockObject)
        {
            if (!_randomNumbers.TryGetValue(max, out var numbers))
            {
                numbers = new Queue<int>();
                _randomNumbers.Add(max, numbers);
            }

            foreach (var value in values)
                numbers.Enqueue(value);
        }
    }
}