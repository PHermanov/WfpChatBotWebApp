using System.Text.Json;
using WfpChatBotWebApp.TelegramBot.Services.InternetSearch.Models;

namespace WfpChatBotWebApp.TelegramBot.Services.InternetSearch;

public interface IInternetSearchService
{
    public Task<SearchResultModel[]?> Search(string query, int take, CancellationToken cancellationToken);
}

public class GoogleSearchService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleSearchService> logger) : IInternetSearchService
{

    public async Task<SearchResultModel[]?> Search(string query, int take, CancellationToken cancellationToken)
    {
        var googleKeys = configuration["GoogleApiKey"];

        if (string.IsNullOrEmpty(googleKeys))
        {
            logger.LogError("GoogleSearch: GoogleApiKey is null");
            return null;
        }

        var split = googleKeys.Split([' '], StringSplitOptions.RemoveEmptyEntries);

        if (split.Length < 2 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1]))
        {
            logger.LogError("GoogleSearch: GoogleApiKey corrupted");
            return null;
        }

        var urlParams = $"v1?key={split[0]}&cx={split[1]}&q={query}";

        var httpClient = httpClientFactory.CreateClient("Google");
        var httpResponseMessage = await httpClient.GetAsync(urlParams, cancellationToken);

        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            logger.LogInformation("GoogleSearch: Google returned not success status");
            return null;
        }

        await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken);

        var searchResults = await JsonSerializer.DeserializeAsync<GoogleResponseModel>(contentStream, cancellationToken: cancellationToken);
        if (searchResults == null || searchResults.Items.Count == 0)
        {
            logger.LogInformation("GoogleSearch: Parsed 0 results");
            return null;
        }

        logger.LogInformation("GoogleSearch: Parsed {SearchResultsCount} results", searchResults.Items.Count);

        return searchResults.Items.Take(take)?.ToArray();
    }
}
