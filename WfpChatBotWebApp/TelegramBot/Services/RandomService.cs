using System.Text.Json;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IRandomService
{
    Task<int> GetRandomNumber(int max);
}

public class RandomService(IHttpClientFactory httpClientFactory, 
    IRandomNumbersQueueService numbersQueueService,  
    IConfiguration configuration,
    ILogger<RandomService> logger) : IRandomService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    
    public async Task<int> GetRandomNumber(int max)
    {
        if (numbersQueueService.CanPeek(max))
        {
            logger.LogInformation("Getting random number from queue");
            return numbersQueueService.GetNextRandomNumber(max);
        }
        else
        {
            await FillRandomNumbersQueue(max);
            return numbersQueueService.GetNextRandomNumber(max);
        }
    }

    private async Task FillRandomNumbersQueue(int max)
    {
        logger.LogInformation("Filling random numbers queue from random.org");
        var requestBody = new
        {
            jsonrpc = "2.0",
            method = "generateIntegers",
            @params = new
            {
                apiKey = configuration["RandomOrgKey"],
                n = 10,
                min = 0,
                max = max-1,
                replacement = true
            },
            id = 42
        };

        var httpClient = httpClientFactory.CreateClient("Random");
        var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(configuration["RandomOrgUri"], content);

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var dataElement = doc.RootElement.GetProperty("result").GetProperty("random").GetProperty("data");
            var rawData = dataElement.GetRawText();
            
            logger.LogInformation("Received random numbers from random.org: {Data}", rawData);
            
            var data = JsonSerializer.Deserialize<int[]>(rawData, SerializerOptions);

            if (data is { Length: > 0 })
            {
                numbersQueueService.EnqueueRange(max, data);
                return;
            }
        }

        logger.LogError("Failed to get numbers from random.org");
        numbersQueueService.EnqueueRange(max, [new Random().Next(max)]);
    }
}