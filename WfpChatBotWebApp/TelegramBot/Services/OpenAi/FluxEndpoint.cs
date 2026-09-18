namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

/// <summary>
/// Resolves the Azure FLUX (Black Forest Labs provider) REST endpoint from a configured base URL and model id.
/// Mirrors the resolution rules previously provided by the ElBruno.Text2Image.Foundry SDK so existing
/// FoundryUrl/FluxModelName configuration keeps working unchanged.
/// </summary>
internal static class FluxEndpoint
{
    public static Uri BuildUrl(string endpoint, string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The FLUX endpoint must use HTTPS.", nameof(endpoint));

        endpoint = endpoint.TrimEnd('/');
        var uri = new Uri(endpoint);

        // Already a full BFL provider URL; use as-is.
        if (uri.AbsolutePath.Contains("/providers/blackforestlabs/", StringComparison.OrdinalIgnoreCase))
            return new Uri(endpoint);

        var bflModelPath = MapModelToBflPath(modelId);

        // Base resource URL (no path) -> build the full BFL provider path.
        if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
        {
            var baseUrl = ConvertToServicesEndpoint(endpoint);
            return new Uri($"{baseUrl}/providers/blackforestlabs/v1/{bflModelPath}?api-version=preview");
        }

        // An OpenAI-compatible path was configured; convert the host and rebuild the BFL path.
        if (uri.AbsolutePath.Contains("/openai/", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = ConvertToServicesEndpoint($"{uri.Scheme}://{uri.Host}");
            return new Uri($"{baseUrl}/providers/blackforestlabs/v1/{bflModelPath}?api-version=preview");
        }

        // A different custom URL was provided; use it as-is.
        return new Uri(endpoint);
    }

    private static string ConvertToServicesEndpoint(string endpoint) =>
        endpoint.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase)
            ? endpoint.Replace(".openai.azure.com", ".services.ai.azure.com", StringComparison.OrdinalIgnoreCase)
            : endpoint;

    private static string MapModelToBflPath(string modelId) => modelId.ToUpperInvariant() switch
    {
        "FLUX.2-PRO" => "flux-2-pro",
        "FLUX.2-FLEX" => "flux-2-flex",
        "FLUX-1.1-PRO" or "FLUX.1-PRO" => "flux-pro-1.1",
        "FLUX.1-KONTEXT-PRO" => "flux-1-kontext-pro",
        _ => modelId.ToLowerInvariant().Replace(".", "-")
    };
}
