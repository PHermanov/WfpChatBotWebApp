using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class RandomServiceTests
{
    [Fact]
    public async Task GetRandomNumber_WhenQueueContainsValue_DoesNotCallRandomOrg()
    {
        var queue = new RandomNumbersQueueService();
        queue.EnqueueRange(3, [2]);
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException("HTTP should not be called.")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, queue);

        var result = await service.GetRandomNumber(3);

        Assert.Equal(2, result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenRandomOrgSucceeds_CachesBatchAndSendsExpectedRequest()
    {
        Uri? requestUri = null;
        string? requestBody = null;
        using var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestUri = request.RequestUri;
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return CreateRandomOrgResponse([2, 1, 0]);
        });
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var first = await service.GetRandomNumber(3);
        var second = await service.GetRandomNumber(3);

        Assert.Equal(2, first);
        Assert.Equal(1, second);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(RandomOrgUri, requestUri);

        using var requestJson = JsonDocument.Parse(requestBody!);
        var root = requestJson.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal("generateIntegers", root.GetProperty("method").GetString());
        Assert.Equal(RandomOrgApiKey, root.GetProperty("params").GetProperty("apiKey").GetString());
        Assert.Equal(10, root.GetProperty("params").GetProperty("n").GetInt32());
        Assert.Equal(0, root.GetProperty("params").GetProperty("min").GetInt32());
        Assert.Equal(2, root.GetProperty("params").GetProperty("max").GetInt32());
        Assert.True(root.GetProperty("params").GetProperty("replacement").GetBoolean());
    }

    [Fact]
    public async Task GetRandomNumber_WhenRandomOrgReturnsFailureStatus_FallsBackInRange()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetRandomNumber(4);

        Assert.InRange(result, 0, 3);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenRandomOrgRequestFails_FallsBackInRange()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Random.org is unavailable.")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetRandomNumber(5);

        Assert.InRange(result, 0, 4);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenRandomOrgTimesOut_FallsBackInRange()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Random.org timed out.")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetRandomNumber(6);

        Assert.InRange(result, 0, 5);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenRandomOrgReturnsMalformedJson_FallsBackInRange()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        }));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetRandomNumber(7);

        Assert.InRange(result, 0, 6);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task GetRandomNumber_WhenRandomOrgReturnsOutOfRangeData_FallsBackInRange(int invalidValue)
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateRandomOrgResponse([invalidValue])));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetRandomNumber(3);

        Assert.InRange(result, 0, 2);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenRandomOrgReturnsEmptyData_FallsBackInRange()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(CreateRandomOrgResponse([])));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetRandomNumber(3);

        Assert.InRange(result, 0, 2);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenConfigurationIsMissing_FallsBackWithoutCallingHttp()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException("HTTP should not be called.")));
        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var service = CreateService(httpClient, configuration: configuration);

        var result = await service.GetRandomNumber(3);

        Assert.InRange(result, 0, 2);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenMaximumIsNotPositive_ThrowsWithoutCallingHttp()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException("HTTP should not be called.")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetRandomNumber(0));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenCallerCancelsRequest_PropagatesCancellation()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateRandomOrgResponse([0]);
        });
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);
        using var cancellation = new CancellationTokenSource(TestTimeout);

        var resultTask = service.GetRandomNumber(3, cancellation.Token);
        await requestStarted.Task.WaitAsync(TestTimeout);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRandomNumber_WhenCalledConcurrently_PerformsSingleRefill()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestStarted.SetResult();
            await releaseResponse.Task.WaitAsync(cancellationToken);
            return CreateRandomOrgResponse(Enumerable.Range(0, 10).ToArray());
        });
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        using var cancellation = new CancellationTokenSource(TestTimeout);
        var resultTasks = Enumerable.Range(0, 10)
            .Select(_ => service.GetRandomNumber(10, cancellation.Token))
            .ToArray();

        await requestStarted.Task.WaitAsync(TestTimeout);
        releaseResponse.SetResult();
        var results = await Task.WhenAll(resultTasks).WaitAsync(TestTimeout);

        Assert.Equal(Enumerable.Range(0, 10), results.Order());
        Assert.Equal(1, handler.CallCount);
    }

    private static RandomService CreateService(
        HttpClient httpClient,
        IRandomNumbersQueueService? queue = null,
        IConfiguration? configuration = null)
    {
        configuration ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RandomOrgKey"] = RandomOrgApiKey,
                ["RandomOrgUri"] = RandomOrgUri.ToString()
            })
            .Build();

        return new RandomService(
            new StubHttpClientFactory(httpClient),
            queue ?? new RandomNumbersQueueService(),
            configuration,
            NullLogger<RandomService>.Instance);
    }

    private static HttpResponseMessage CreateRandomOrgResponse(int[] values)
    {
        var json = JsonSerializer.Serialize(new
        {
            result = new
            {
                random = new
                {
                    data = values
                }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return sendAsync(request, cancellationToken);
        }
    }

    private static readonly Uri RandomOrgUri = new("https://random.test/json-rpc");
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string RandomOrgApiKey = "test-api-key";
}
