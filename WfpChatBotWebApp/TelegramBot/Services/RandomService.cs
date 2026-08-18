using System.Text.Json;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IRandomService
{
    Task<int> GetRandomNumber(int max, CancellationToken cancellationToken = default);
}

public class RandomService(IHttpClientFactory httpClientFactory, 
    IRandomNumbersQueueService numbersQueueService,  
    IConfiguration configuration,
    ILogger<RandomService> logger) : IRandomService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _refillLock = new(1, 1);
    
    public async Task<int> GetRandomNumber(int max, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        if (numbersQueueService.TryDequeue(max, out var value))
            return value;

        await _refillLock.WaitAsync(cancellationToken);
        try
        {
            if (numbersQueueService.TryDequeue(max, out value))
                return value;

            var values = await GetRandomNumbers(max, cancellationToken);
            numbersQueueService.EnqueueRange(max, values);

            if (numbersQueueService.TryDequeue(max, out value))
                return value;

            throw new InvalidOperationException("The random-number queue was empty after a refill.");
        }
        finally
        {
            _refillLock.Release();
        }
    }

    private async Task<int[]> GetRandomNumbers(int max, CancellationToken cancellationToken)
    {
        logger.LogInformation("RandomService: Filling random numbers queue from random.org");

        var apiKey = configuration["RandomOrgKey"];
        var requestUri = configuration["RandomOrgUri"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(requestUri))
        {
            logger.LogError("RandomService: Random.org configuration is missing");
            return GetFallback(max);
        }

        var requestBody = new
        {
            jsonrpc = "2.0",
            method = "generateIntegers",
            @params = new
            {
                apiKey,
                n = 10,
                min = 0,
                max = max - 1,
                replacement = true
            },
            id = 42
        };

        try
        {
            var httpClient = httpClientFactory.CreateClient("Random");
            using var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("RandomService: Random.org returned status code {StatusCode}", response.StatusCode);
                return GetFallback(max);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var randomOrgResponse = await JsonSerializer.DeserializeAsync<RandomOrgResponse>(
                responseStream,
                SerializerOptions,
                cancellationToken);
            var data = randomOrgResponse?.Result?.Random?.Data;

            if (data is { Length: > 0 } && data.All(value => value >= 0 && value < max))
                return data;

            logger.LogWarning("RandomService: Random.org returned missing or invalid data");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("RandomService: Random.org request timed out");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "RandomService: Random.org request failed");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "RandomService: Random.org response was not valid JSON");
        }

        return GetFallback(max);
    }

    private static int[] GetFallback(int max) => [Random.Shared.Next(max)];

    private sealed record RandomOrgResponse(RandomOrgResult? Result);
    private sealed record RandomOrgResult(RandomOrgData? Random);
    private sealed record RandomOrgData(int[]? Data);
}