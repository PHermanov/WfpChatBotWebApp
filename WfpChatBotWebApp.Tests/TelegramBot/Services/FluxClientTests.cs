using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

/// <summary>
/// Provider-level tests for the raw HttpClient FLUX transport (FluxEndpoint + FluxClient, wired through
/// FluxImageService). Covers both creation and editing since they share almost all transport behavior.
/// </summary>
public class FluxClientTests
{
    [Fact]
    public async Task Generate_SendsDocumentedCreationPayloadWithoutInputImage()
    {
        using var handler = new ImageHttpHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/providers/blackforestlabs/v1/flux-2-pro", request.RequestUri!.AbsolutePath);
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("api-key")));
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var body = document.RootElement;
            Assert.Equal("FLUX.2-pro", body.GetProperty("model").GetString());
            Assert.Equal("A red fox in an autumn forest", body.GetProperty("prompt").GetString());
            Assert.Equal("png", body.GetProperty("output_format").GetString());
            Assert.Equal(1024, body.GetProperty("width").GetInt32());
            Assert.Equal(1024, body.GetProperty("height").GetInt32());
            Assert.Equal(1, body.GetProperty("num_images").GetInt32());
            Assert.False(body.TryGetProperty("input_image", out _));
            return ImageTestData.Json(ImageResult());
        });
        var images = await ImageEditingTests.Collect(Service(handler).CreateImage(
            "A red fox in an autumn forest", TestContext.Current.CancellationToken));
        Assert.Equal(ImageTestData.Png, Assert.Single(images));
    }

    [Fact]
    public async Task Edit_SendsDocumentedEditingPayloadWithRawSourceBytesAndNoSizeFields()
    {
        var source = Convert.FromHexString("89504E470D0A1A0A");
        using var handler = new ImageHttpHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/providers/blackforestlabs/v1/flux-2-pro", request.RequestUri!.AbsolutePath);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var body = document.RootElement;
            Assert.Equal("FLUX.2-pro", body.GetProperty("model").GetString());
            Assert.Equal("Only add a red hat; preserve the subject", body.GetProperty("prompt").GetString());
            Assert.Equal("png", body.GetProperty("output_format").GetString());
            Assert.False(body.TryGetProperty("n", out _));
            Assert.False(body.TryGetProperty("width", out _));
            Assert.False(body.TryGetProperty("height", out _));
            Assert.False(body.TryGetProperty("num_images", out _));
            var encoded = body.GetProperty("input_image").GetString()!;
            Assert.DoesNotContain("data:", encoded);
            Assert.Equal(source, Convert.FromBase64String(encoded));
            return ImageTestData.Json(ImageResult());
        });
        var images = await ImageEditingTests.Collect(Service(handler).EditImage("Only add a red hat; preserve the subject",
            new BinaryData(source), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ImageTestData.Png, Assert.Single(images));
    }

    [Theory]
    [InlineData("https://example.services.ai.azure.com", "example.services.ai.azure.com", "/providers/blackforestlabs/v1/flux-2-pro")]
    [InlineData("https://example.openai.azure.com", "example.services.ai.azure.com", "/providers/blackforestlabs/v1/flux-2-pro")]
    [InlineData("https://example.openai.azure.com/openai/v1", "example.services.ai.azure.com", "/providers/blackforestlabs/v1/flux-2-pro")]
    [InlineData("https://example.services.ai.azure.com/providers/blackforestlabs/v1/flux-2-pro?api-version=preview",
        "example.services.ai.azure.com", "/providers/blackforestlabs/v1/flux-2-pro")]
    public async Task Generate_ResolvesEndpointFromConfiguredFoundryUrl(string foundryUrl, string expectedHost, string expectedPath)
    {
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.Equal(expectedHost, request.RequestUri!.Host);
            Assert.Equal(expectedPath, request.RequestUri.AbsolutePath);
            return Task.FromResult(ImageTestData.Json(ImageResult()));
        });
        await ImageEditingTests.Collect(Service(handler, foundryUrl).CreateImage("cup", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Generate_RejectsNonHttpsEndpoint()
    {
        var service = Service(new ImageHttpHandler((_, _) => throw new InvalidOperationException("Unexpected HTTP")), "http://example.services.ai.azure.com");
        await Assert.ThrowsAsync<ArgumentException>(() => ImageEditingTests.Collect(service.CreateImage("cup", TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData(false, "operation-location", true)]
    [InlineData(false, "Location", false)]
    [InlineData(true, "operation-location", true)]
    [InlineData(true, "Location", false)]
    public async Task PollsAcceptedAzureOperations(bool edit, string header, bool nested)
    {
        var requests = 0;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            requests++;
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("api-key")));
            if (request.Method == HttpMethod.Post)
            {
                var accepted = ImageTestData.Json(new { });
                accepted.StatusCode = HttpStatusCode.Accepted;
                accepted.Headers.Add(header, "/operations/op-1");
                return Task.FromResult(accepted);
            }
            Assert.Equal("https://example.services.ai.azure.com/operations/op-1", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(nested
                ? ImageTestData.Json(new { status = "succeeded", result = ImageResult() })
                : ImageTestData.Json(new { status = "completed", data = new[] { new { b64_json = Convert.ToBase64String(ImageTestData.Png) } } }));
        });
        var result = await Invoke(handler, edit);
        Assert.Equal(ImageTestData.Png, Assert.Single(result));
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadsResultUrlWithoutSendingApiKey(bool edit)
    {
        var downloads = 0;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
                return Task.FromResult(ImageTestData.Json(new { data = new[] { new { url = "https://images.example/result.png" } } }));
            Assert.Equal("images.example", request.RequestUri!.Host);
            Assert.False(request.Headers.Contains("api-key"));
            Assert.Null(request.Headers.Authorization);
            downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(ImageTestData.Png) });
        });
        Assert.Equal(ImageTestData.Png, Assert.Single(await Invoke(handler, edit)));
        Assert.Equal(1, downloads);
    }

    [Theory]
    [InlineData(false, "https://other.example/operations/1")]
    [InlineData(false, "http://example.services.ai.azure.com/operations/1")]
    [InlineData(false, "https://user@example.services.ai.azure.com/operations/1")]
    [InlineData(true, "https://other.example/operations/1")]
    public async Task RejectsUntrustedOrInsecurePollingLocations(bool edit, string location)
    {
        var requests = 0;
        using var handler = new ImageHttpHandler((_, _) =>
        {
            requests++;
            var response = ImageTestData.Json(new { });
            response.StatusCode = HttpStatusCode.Accepted;
            response.Headers.Add("operation-location", location);
            return Task.FromResult(response);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(handler, edit));
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsAcceptedResponseWithoutPollingLocation(bool edit)
    {
        using var handler = new ImageHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(handler, edit));
    }

    [Theory]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":[{\"b64_json\":\"\"}]}")]
    [InlineData("{\"data\":[{\"url\":\"http://images.example/image.png\"}]}")]
    public async Task RejectsMissingOrUnsafeImageResults(string json)
    {
        using var handler = new ImageHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(handler, edit: false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(handler, edit: true));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(302)]
    public async Task DoesNotExposeErrorResponseBodies(int code)
    {
        using var handler = new ImageHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)
        {
            Content = new StringContent("Private image or credential details")
        }));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Invoke(handler, edit: true));
        Assert.DoesNotContain("Private", error.Message);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task StopsWhenAzureOperationFails(string status)
    {
        using var handler = new ImageHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(ImageTestData.Json(new { status, error = new { message = "Private provider detail" } }));
            var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
            accepted.Headers.Add("operation-location", "/operations/op-1");
            return Task.FromResult(accepted);
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(handler, edit: true));
        Assert.DoesNotContain("Private", error.Message);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PropagatesCallerCancellationDuringSubmissionOrPolling(bool edit, bool polling)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new ImageHttpHandler(async (request, token) =>
        {
            if (polling && request.Method == HttpMethod.Post)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Add("operation-location", "/operations/op-1");
                return accepted;
            }
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Expected cancellation");
        });
        var service = Service(handler);
        var result = edit
            ? ImageEditingTests.Collect(service.EditImage("edit", new BinaryData(ImageTestData.Png), cancellationToken: cancellation.Token))
            : ImageEditingTests.Collect(service.CreateImage("cup", cancellation.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
    }

    private static object ImageResult() => new { data = new[] { new { b64_json = Convert.ToBase64String(ImageTestData.Png) } } };

    private static Task<List<byte[]>> Invoke(HttpMessageHandler handler, bool edit) => edit
        ? ImageEditingTests.Collect(Service(handler).EditImage("edit", new BinaryData(ImageTestData.Png), cancellationToken: TestContext.Current.CancellationToken))
        : ImageEditingTests.Collect(Service(handler).CreateImage("cup", TestContext.Current.CancellationToken));

    private static FluxImageService Service(HttpMessageHandler handler, string foundryUrl = "https://example.services.ai.azure.com") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FoundryUrl"] = foundryUrl, ["OpenAiKey"] = "test-key", ["FluxModelName"] = "FLUX.2-pro"
        }).Build(), new ImageHttpFactory(handler));
}
