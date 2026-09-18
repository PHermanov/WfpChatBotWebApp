using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WfpChatBotWebApp.TelegramBot.Services.OpenAi;

/// <summary>
/// Raw HttpClient transport for the Azure FLUX.2 (Black Forest Labs provider) REST API.
/// Handles both text-to-image creation and reference-image editing against the documented contract:
/// https://learn.microsoft.com/en-us/azure/foundry/foundry-models/how-to/use-foundry-models-flux
/// </summary>
internal static class FluxClient
{
    private const int MaxPollAttempts = 60;
    private static readonly TimeSpan InitialPollDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxPollDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(5);

    public static Task<byte[]> GenerateAsync(HttpClient client, Uri endpoint, string apiKey, string model,
        string prompt, CancellationToken cancellationToken) =>
        SubmitAsync(client, endpoint, apiKey, new
        {
            model,
            prompt,
            width = 1024,
            height = 1024,
            output_format = "png",
            num_images = 1
        }, cancellationToken);

    public static Task<byte[]> EditAsync(HttpClient client, Uri endpoint, string apiKey, string model,
        string prompt, string inputImage, CancellationToken cancellationToken) =>
        SubmitAsync(client, endpoint, apiKey, new
        {
            model,
            prompt,
            output_format = "png",
            input_image = inputImage
        }, cancellationToken);

    private static async Task<byte[]> SubmitAsync(HttpClient client, Uri endpoint, string apiKey, object payload,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeout);
        try
        {
            // JsonContent does not precompute a length, which causes chunked request framing without
            // Content-Length. Azure's gateway rejects that (x-ms-error-code: no_content_length_header),
            // so serialize the body ourselves into StringContent, which sets Content-Length explicitly.
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };
            request.Headers.Add("api-key", apiKey);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.Accepted)
                return await PollAsync(client, endpoint, response, apiKey, timeout.Token);
            using var document = await ReadJsonAsync(response, timeout.Token);
            return await ReadImageAsync(client, document.RootElement, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException("FLUX request exceeded five minutes.");
        }
    }

    private static async Task<byte[]> PollAsync(HttpClient client, Uri endpoint, HttpResponseMessage submission,
        string apiKey, CancellationToken cancellationToken)
    {
        var location = submission.Headers.TryGetValues("operation-location", out var values)
            ? values.FirstOrDefault()
            : null;
        location ??= submission.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location) || !Uri.TryCreate(endpoint, location, out var uri))
            throw new InvalidOperationException("FLUX accepted the request without a valid polling location.");
        if (!IsHttps(uri) || Uri.Compare(endpoint, uri, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) != 0)
            throw new InvalidOperationException("FLUX polling must remain on the configured Azure origin.");

        var delay = InitialPollDelay;
        for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(delay, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("api-key", apiKey);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var status = root.TryGetProperty("status", out var statusValue) ? statusValue.GetString()?.ToLowerInvariant() : null;
                if (status is "failed" or "canceled" or "cancelled")
                    throw new InvalidOperationException("FLUX operation failed or was cancelled.");
                if (status is "succeeded" or "complete" or "completed" || root.TryGetProperty("data", out _))
                {
                    var result = root.TryGetProperty("result", out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : root;
                    return await ReadImageAsync(client, result, cancellationToken);
                }
            }
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxPollDelay.TotalSeconds));
        }
        throw new TimeoutException("FLUX did not finish within the polling limit.");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task<byte[]> ReadImageAsync(HttpClient client, JsonElement result, CancellationToken cancellationToken)
    {
        if (!result.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            throw new InvalidOperationException("FLUX returned no image data.");

        var image = data[0];
        byte[] bytes;
        if (image.TryGetProperty("b64_json", out var encoded) && encoded.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(encoded.GetString()))
        {
            bytes = Convert.FromBase64String(encoded.GetString()!);
        }
        else if (image.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) && IsHttps(uri))
        {
            // Downloaded without the API key: the image host is not necessarily the trusted Azure origin.
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("FLUX returned neither image bytes nor a valid HTTPS image URL.");
        }
        if (bytes.Length == 0)
            throw new InvalidOperationException("FLUX returned an empty image.");
        return bytes;
    }

    private static bool IsHttps(Uri uri) => uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
}
