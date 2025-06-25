using System.Text.Json;
using System.Text;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface ISoraService
{
    Task<Stream?> GetVideo(string prompt, ushort duration, CancellationToken cancellationToken);
}

public class SoraService(
    IHttpClientFactory httpClientFactory, 
    IConfiguration configuration,
    ILogger<SoraService> logger) : ISoraService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    
    public async Task<Stream?> GetVideo(string prompt, ushort duration, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = configuration["SoraUrl"];
            var key = configuration["SoraKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            {
                logger.LogError("Sora parameters not set");
                return null;
            }
            
            var body = new
            {
                prompt = prompt.Trim(),
                width = 480,
                height = 480,
                n_seconds = duration,
                model = "wfp-sora"
            };

            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/openai/v1/video/generations/jobs?api-version=preview");
            createRequest.Headers.Add("api-key", key);
            createRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var httpClient = httpClientFactory.CreateClient("Sora");

            // 1. Create a video generation job
            var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
            var createResponseJson = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            using var createResponseDoc = JsonDocument.Parse(createResponseJson);
            var jobId = createResponseDoc.RootElement.GetProperty("id").GetString();

            logger.LogInformation("Received job ID: {JobId}", jobId);
            
            if (string.IsNullOrEmpty(jobId))
                return null;
            
            // 2. Poll for job status
            var statusUrl = $"{endpoint}/openai/v1/video/generations/jobs/{jobId}?api-version=preview";

            var status = string.Empty;
            JsonDocument statusDoc = null!;
            while (status is not ("succeeded" or "failed" or "cancelled"))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Wait before polling again
                
                var statusRequest = new HttpRequestMessage(HttpMethod.Get, statusUrl);
                statusRequest.Headers.Add("api-key", key);
                
                var statusResponse = await httpClient.SendAsync(statusRequest, cancellationToken);
                var statusResponseJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
                statusDoc = JsonDocument.Parse(statusResponseJson);
                status = statusDoc.RootElement.GetProperty("status").GetString();
                
                logger.LogInformation("Job status: {status}", status);
            }
            // 3. Retrieve generated video 
            if (status == "succeeded")
            {
                var generationsElement = statusDoc.RootElement.GetProperty("generations");
                var generations = generationsElement.Deserialize<Generation[]>(SerializerOptions);

                var generationId = generations?.FirstOrDefault()?.Id;

                logger.LogInformation("Generation ID: {GenerationId}", generationId);

                if (!string.IsNullOrEmpty(generationId))
                {
                    var videoUrl = $"{endpoint}/openai/v1/video/generations/{generationId}/content/video?api-version=preview";
                    var videoRequest = new HttpRequestMessage(HttpMethod.Get, videoUrl);
                    videoRequest.Headers.Add("api-key", key);
                    
                    var videoResponse = await httpClient.SendAsync(videoRequest, cancellationToken);
                    return await videoResponse.Content.ReadAsStreamAsync(cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in {Name}", nameof(SoraService));
        }
        return null;
    }

    private class Generation
    {
        public required string Id { get; set; }
    }
}