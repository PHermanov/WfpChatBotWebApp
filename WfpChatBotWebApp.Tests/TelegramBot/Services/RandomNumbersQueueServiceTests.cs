using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class RandomNumbersQueueServiceTests
{
    [Fact]
    public void TryDequeue_WhenQueueDoesNotExist_ReturnsFalse()
    {
        var service = new RandomNumbersQueueService();

        var result = service.TryDequeue(3, out var value);

        Assert.False(result);
        Assert.Equal(0, value);
    }

    [Fact]
    public void EnqueueRange_DequeuesValuesInFifoOrder()
    {
        var service = new RandomNumbersQueueService();
        service.EnqueueRange(3, [2, 0, 1]);

        Assert.True(service.TryDequeue(3, out var first));
        Assert.True(service.TryDequeue(3, out var second));
        Assert.True(service.TryDequeue(3, out var third));
        Assert.False(service.TryDequeue(3, out _));

        Assert.Equal([2, 0, 1], [first, second, third]);
    }

    [Fact]
    public void EnqueueRange_AfterQueueIsDrained_CreatesAUsableQueue()
    {
        var service = new RandomNumbersQueueService();
        service.EnqueueRange(2, [0]);
        Assert.True(service.TryDequeue(2, out _));

        service.EnqueueRange(2, [1]);

        Assert.True(service.TryDequeue(2, out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void Queues_AreSeparatedByMaximum()
    {
        var service = new RandomNumbersQueueService();
        service.EnqueueRange(2, [1]);
        service.EnqueueRange(3, [2]);

        Assert.True(service.TryDequeue(3, out var largerMaximumValue));
        Assert.Equal(2, largerMaximumValue);
        Assert.True(service.TryDequeue(2, out var smallerMaximumValue));
        Assert.Equal(1, smallerMaximumValue);
    }

    [Fact]
    public void EnqueueRange_WithEmptyValues_DoesNotCreateAQueue()
    {
        var service = new RandomNumbersQueueService();

        service.EnqueueRange(3, []);

        Assert.False(service.TryDequeue(3, out _));
    }

    [Fact]
    public void Operations_WithNonPositiveMaximum_Throw()
    {
        var service = new RandomNumbersQueueService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.TryDequeue(0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.EnqueueRange(-1, [0]));
    }

    [Fact]
    public void EnqueueRange_WithNullValues_Throws()
    {
        var service = new RandomNumbersQueueService();

        Assert.Throws<ArgumentNullException>(() => service.EnqueueRange(3, null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void EnqueueRange_WithValueOutsideMaximum_ThrowsWithoutEnqueueing(int value)
    {
        var service = new RandomNumbersQueueService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.EnqueueRange(3, [0, value]));
        Assert.False(service.TryDequeue(3, out _));
    }

    [Fact]
    public async Task EnqueueAndDequeue_WhenCalledConcurrently_RemainConsistent()
    {
        const int workerCount = 16;
        const int iterations = 500;
        var service = new RandomNumbersQueueService();

        var workers = Enumerable.Range(0, workerCount).Select(worker => Task.Run(() =>
        {
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                service.EnqueueRange(workerCount, [worker]);
                Assert.True(service.TryDequeue(workerCount, out var value));
                Assert.InRange(value, 0, workerCount - 1);
            }
        }));

        await Task.WhenAll(workers);

        Assert.False(service.TryDequeue(workerCount, out _));
    }
}
