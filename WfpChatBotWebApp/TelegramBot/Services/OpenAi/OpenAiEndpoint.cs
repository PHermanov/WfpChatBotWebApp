namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

/// <summary>
/// Resolves the Azure OpenAI v1 Responses endpoint from the configured resource URL, so a
/// <c>FoundryUrl</c> with or without an <c>/openai[/v1]</c> suffix keeps working unchanged.
/// </summary>
public static class OpenAiEndpoint
{
    public static Uri ForResponses(string foundryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(foundryUrl);

        var endpoint = new UriBuilder(foundryUrl);
        var path = endpoint.Path.TrimEnd('/');
        if (!path.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            path += path.EndsWith("/openai", StringComparison.OrdinalIgnoreCase)
                ? "/v1"
                : "/openai/v1";
        }

        endpoint.Path = path;
        return endpoint.Uri;
    }
}
