using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Responses;
using Telegram.Bot;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.Persistence.Models;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

#pragma warning disable OPENAI001 // Exercise the installed experimental Responses APIs.

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class OpenAiChatServiceTests
{
    [Theory]
    [InlineData("https://example.openai.azure.com", "https://example.openai.azure.com/openai/v1")]
    [InlineData("https://example.openai.azure.com/", "https://example.openai.azure.com/openai/v1")]
    [InlineData("https://example.openai.azure.com/openai", "https://example.openai.azure.com/openai/v1")]
    [InlineData("https://example.openai.azure.com/openai/v1/", "https://example.openai.azure.com/openai/v1")]
    [InlineData("https://gateway.example/prefix/", "https://gateway.example/prefix/openai/v1")]
    [InlineData("https://gateway.example/prefix/openai/v1", "https://gateway.example/prefix/openai/v1")]
    public void Endpoint_ResolvesToV1ResponsesEndpoint(string configuredEndpoint, string expectedEndpoint)
    {
        Assert.Equal(new Uri(expectedEndpoint), OpenAiEndpoint.ForResponses(configuredEndpoint));
    }

    [Fact]
    public void AddOpenAiClients_RegistersSingletonClientsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenAiUrl"] = "https://example.openai.azure.com",
            ["OpenAiKey"] = "test-key",
            ["OpenAiChatModelName"] = "gpt-6-astra",
            ["OpenAiAudioModelName"] = "test-audio"
        }).Build();

        using var provider = new ServiceCollection()
            .Configure<OpenAiOptions>(configuration)
            .AddOpenAiClients()
            .BuildServiceProvider();

        var responsesClient = provider.GetRequiredService<ResponsesClient>();

        Assert.Equal(new Uri("https://example.openai.azure.com/openai/v1"), responsesClient.Endpoint);
        Assert.Same(responsesClient, provider.GetRequiredService<ResponsesClient>());
        Assert.Same(provider.GetRequiredService<AudioClient>(), provider.GetRequiredService<AudioClient>());
    }

    [Fact]
    public async Task ProcessMessage_StreamsTextAndSendsResponsesOptionsAndStrictTools()
    {
        using var harness = new Harness(
            TextDelta("<b>Hello") + TextDelta(" world</b>") + Completed(Message("<b>Hello world</b>")));

        var results = await Collect(harness.Service.ProcessMessage(
            Guid.NewGuid(), 10, [Request("Hello", 42)], TestContext.Current.CancellationToken));

        Assert.Equal(["<b>Hello", "<b>Hello world</b>", "<b>Hello world</b>"], results.Select(result => result.Content));
        Assert.Equal([false, false, true], results.Select(result => result.ContentComplete));
        Assert.All(results, result => Assert.Equal(OpenAiContentType.Text, result.ContentType));
        Assert.Equal(new Uri("https://example.openai.azure.com/openai/v1/responses"), Assert.Single(harness.RequestUris));
        var body = Assert.Single(harness.Requests);
        Assert.Equal("gpt-6-astra", body.GetProperty("model").GetString());
        Assert.True(body.GetProperty("stream").GetBoolean());
        Assert.False(body.GetProperty("store").GetBoolean());
        Assert.Equal("high", body.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("reasoning.encrypted_content", body.GetProperty("include").EnumerateArray().Select(item => item.GetString()));
        Assert.False(body.TryGetProperty("reasoning_effort", out _));
        Assert.False(body.TryGetProperty("previous_response_id", out _));
        Assert.Equal("auto", body.GetProperty("tool_choice").GetString());
        var tools = body.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Equal(["CreateImage", "EditImage"], tools.Select(tool => tool.GetProperty("name").GetString()));
        var tool = tools[0];
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal("CreateImage", tool.GetProperty("name").GetString());
        Assert.True(tool.GetProperty("strict").GetBoolean());
        Assert.False(tool.GetProperty("parameters").GetProperty("additionalProperties").GetBoolean());
        var input = body.GetProperty("input");
        Assert.Equal("system", input[0].GetProperty("role").GetString());
        Assert.Contains("Test prompt", input[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("user", input[1].GetProperty("role").GetString());
        Assert.Equal("Telegram UserId: 42\nHello", input[1].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.False(input[1].TryGetProperty("name", out _));
    }

    [Theory]
    [InlineData("89504E470D0A1A0A", "image/png")]
    [InlineData("FFD8FF", "image/jpeg")]
    [InlineData("524946460000000057454250", "image/webp")]
    [InlineData("474946", "image/gif")]
    public async Task ProcessMessage_SendsImagesAsDataUrisWithHighDetail(string hex, string mediaType)
    {
        using var harness = new Harness(Completed(Message("An image.")));
        var bytes = Convert.FromHexString(hex);
        var request = Request("Describe this", 42);
        request.Image = BinaryData.FromBytes(bytes);

        await Collect(harness.Service.ProcessMessage(Guid.NewGuid(), 10, [request], TestContext.Current.CancellationToken));

        var image = Assert.Single(harness.Requests).GetProperty("input")[1].GetProperty("content")[1];
        Assert.Equal("input_image", image.GetProperty("type").GetString());
        Assert.Equal($"data:{mediaType};base64,{Convert.ToBase64String(bytes)}", image.GetProperty("image_url").GetString());
        Assert.Equal("high", image.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ProcessMessage_KeepsReferencedBotTextAndImageInSupportedRoles()
    {
        using var harness = new Harness(Completed(Message("A reply.")));
        var referencedMessage = Request("My picture", 123456);
        referencedMessage.Image = BinaryData.FromBytes(new byte[] { 0xFF, 0xD8, 0xFF });

        await Collect(harness.Service.ProcessMessage(Guid.NewGuid(), 10,
            [referencedMessage, Request("Change it", 42)], TestContext.Current.CancellationToken));

        var input = Assert.Single(harness.Requests).GetProperty("input");
        Assert.Equal("assistant", input[1].GetProperty("role").GetString());
        Assert.Equal("output_text", input[1].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Contains("My picture", input[1].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("user", input[2].GetProperty("role").GetString());
        Assert.Contains("123456", input[2].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("input_image", input[2].GetProperty("content")[1].GetProperty("type").GetString());
        Assert.Contains("Change it", input[3].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ProcessMessage_ReplaysReasoningAndAllToolResultsWithoutAnExtraModelTurn()
    {
        var firstCall = FunctionCall("call_first", "first");
        using var harness = new Harness(
            Event(new { type = "response.function_call_arguments.delta", sequence_number = 1, item_id = "fc_call_first", output_index = 2, delta = "{\"prompt\":" }) +
            Event(new { type = "response.output_item.done", sequence_number = 2, output_index = 2, item = firstCall }) +
            Completed(Reasoning(), Message("Here are your pictures.", "commentary"), firstCall, FunctionCall("call_second", "second")),
            Completed(Message("Follow-up.")),
            Completed(Message("Separate context.")));
        var context = Guid.NewGuid();

        var first = await Collect(harness.Service.ProcessMessage(context, 10, [Request("Draw two images", 42)], TestContext.Current.CancellationToken));

        Assert.Single(harness.Requests);
        Assert.Equal(["first", "second"], harness.Images.Prompts);
        Assert.Equal([OpenAiContentType.Text, OpenAiContentType.ImageBytes, OpenAiContentType.ImageBytes], first.Select(result => result.ContentType));
        Assert.Equal(new byte[] { 1, 2, 3 }, first[1].ImageContent);
        Assert.Equal(new byte[] { 4, 5, 6 }, first[2].ImageContent);
        Assert.All(first.Skip(1), result => Assert.Empty(result.Content));
        Assert.Equal(TestContext.Current.CancellationToken, harness.Images.ReceivedCancellationToken);

        await Collect(harness.Service.ProcessMessage(context, 10, [Request("Thanks", 84)], TestContext.Current.CancellationToken));

        var input = harness.Requests[1].GetProperty("input");
        Assert.Equal(["message", "message", "reasoning", "message", "function_call", "function_call", "function_call_output", "function_call_output", "message"],
            input.EnumerateArray().Select(item => item.GetProperty("type").GetString()));
        Assert.Equal("opaque-reasoning", input[2].GetProperty("encrypted_content").GetString());
        Assert.Equal("commentary", input[3].GetProperty("phase").GetString());
        Assert.Equal("call_first", input[6].GetProperty("call_id").GetString());
        Assert.Contains("Image generated", input[6].GetProperty("output").GetString());
        Assert.Equal("call_second", input[7].GetProperty("call_id").GetString());
        Assert.Contains("Image generated", input[7].GetProperty("output").GetString());
        Assert.Equal("Telegram UserId: 84\nThanks", input[8].GetProperty("content")[0].GetProperty("text").GetString());

        await Collect(harness.Service.ProcessMessage(Guid.NewGuid(), 20, [Request("Unrelated", 90)], TestContext.Current.CancellationToken));
        Assert.Equal(2, harness.Requests[2].GetProperty("input").GetArrayLength());
    }

    [Fact]
    public async Task ProcessMessage_SurfacesRefusalText()
    {
        using var harness = new Harness(
            Event(new { type = "response.refusal.delta", sequence_number = 1, item_id = "msg_test", output_index = 0, content_index = 0, delta = "Cannot help." }) +
            Completed(new { type = "message", id = "msg_test", role = "assistant", status = "completed", content = new[] { new { type = "refusal", refusal = "Cannot help." } } }));

        var results = await Collect(harness.Service.ProcessMessage(Guid.NewGuid(), 10, [Request("Hello", 42)], TestContext.Current.CancellationToken));

        Assert.Equal(["Cannot help.", "Cannot help."], results.Select(result => result.Content));
        Assert.False(results[0].ContentComplete);
        Assert.True(results[1].ContentComplete);
    }

    [Theory]
    [InlineData("failed", "server_error")]
    [InlineData("incomplete", "max_output_tokens")]
    [InlineData("error", "invalid_request_error")]
    [InlineData("eof", "without a completed response")]
    [InlineData("empty", "no text or function calls")]
    public async Task ProcessMessage_RejectsUnsuccessfulStreamsWithoutExecutingOrRetainingTools(string kind, string expectedError)
    {
        var terminal = kind switch
        {
            "empty" => Completed(Reasoning()),
            "failed" => Event(new { type = "response.failed", sequence_number = 2, response = new { id = "resp_failed", status = "failed", error = new { code = "server_error", message = "Failed." }, output = Array.Empty<object>() } }),
            "incomplete" => Event(new { type = "response.incomplete", sequence_number = 2, response = new { id = "resp_incomplete", status = "incomplete", incomplete_details = new { reason = "max_output_tokens" }, output = Array.Empty<object>() } }),
            "error" => Event(new { type = "error", sequence_number = 2, code = "invalid_request_error", message = "Bad input.", param = "input" }),
            _ => string.Empty
        };
        using var harness = new Harness(TextDelta("Partial") +
            Event(new { type = "response.output_item.done", sequence_number = 1, output_index = 0, item = FunctionCall("call_partial", "first") }) + terminal,
            Completed(Message("Recovered.")));
        var context = Guid.NewGuid();
        List<OpenAiResponse> results = [];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var result in harness.Service.ProcessMessage(context, 10, [Request("Draw", 42)], TestContext.Current.CancellationToken))
                results.Add(result);
        });

        Assert.Contains(expectedError, exception.Message);
        Assert.DoesNotContain(results, result => result.ContentComplete);
        Assert.Empty(harness.Images.Prompts);
        await Collect(harness.Service.ProcessMessage(context, 10, [Request("Retry", 42)], TestContext.Current.CancellationToken));
        Assert.Equal(3, harness.Requests[1].GetProperty("input").GetArrayLength());
    }

    [Theory]
    [InlineData("exception")]
    [InlineData("no-output")]
    [InlineData("empty-bytes")]
    public async Task ProcessMessage_DoesNotRetainUnansweredCallsWhenToolFails(string failureKind)
    {
        using var harness = new Harness(Completed(Reasoning(), FunctionCall("call_failed", "first")), Completed(Message("Recovered.")));
        harness.Images.ReturnNoOutput = failureKind == "no-output";
        harness.Images.ReturnEmptyBytes = failureKind == "empty-bytes";
        harness.Images.Failure = failureKind == "exception" ? new InvalidOperationException("Image provider failed.") : null;
        var context = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Collect(harness.Service.ProcessMessage(
            context, 10, [Request("Draw", 42)], TestContext.Current.CancellationToken)));
        await Collect(harness.Service.ProcessMessage(context, 10, [Request("Hello", 42)], TestContext.Current.CancellationToken));

        var input = harness.Requests[1].GetProperty("input");
        Assert.Equal(3, input.GetArrayLength());
        Assert.All(input.EnumerateArray(), item => Assert.Equal("message", item.GetProperty("type").GetString()));
    }

    [Theory]
    [InlineData("CreateImage", "{}")]
    [InlineData("CreateImage", "{\"prompt\":null}")]
    [InlineData("CreateImage", "{\"prompt\":42}")]
    [InlineData("CreateImage", "{\"prompt\":\" \"}")]
    [InlineData("CreateImage", "[]")]
    [InlineData("Unknown", "{\"prompt\":\"draw\"}")]
    public async Task Tools_RejectInvalidCalls(string name, string arguments)
    {
        var images = new StubImageService();
        var tools = new OpenAiChatToolsService(images, new FakeImages());
        var call = ResponseItem.CreateFunctionCallItem("call_invalid", name, BinaryData.FromString(arguments));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken)));

        Assert.Empty(images.Prompts);
    }

    [Fact]
    public async Task ProcessMessage_PropagatesCancellationToTheStreamingRequest()
    {
        using var harness = new Harness(Completed(Message("Unused."))) { BlockResponses = true };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var resultTask = Collect(harness.Service.ProcessMessage(Guid.NewGuid(), 10, [Request("Hello", 42)], cancellation.Token));
        await harness.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);
        Assert.Empty(harness.Images.Prompts);
    }

    [Fact]
    public async Task EditImage_ReplaysCallIdAndDoesNotReuseSourceForLaterRequests()
    {
        var editCall = new { type = "function_call", id = "fc_edit", call_id = "call_edit", name = "EditImage", arguments = "{\"prompt\":\"add a cup\"}", status = "completed" };
        using var harness = new Harness(Completed(Reasoning(), editCall), Completed(editCall));
        var context = Guid.NewGuid();
        var source = new BinaryData(ImageTestData.Png);
        var result = await Collect(harness.Service.ProcessMessage(context, 10, [Request("Edit", 42)], TestContext.Current.CancellationToken,
            new ImageToolContext(source, "Attach a photo.", "Editing failed.")));
        Assert.Equal(OpenAiContentType.ImageBytes, Assert.Single(result).ContentType);
        Assert.Same(source, Assert.Single(harness.Edits.Sources));
        Assert.Single(harness.Requests);
        result = await Collect(harness.Service.ProcessMessage(context, 10, [Request("Edit again", 42)], TestContext.Current.CancellationToken,
            new ImageToolContext(null, "Attach a photo.", "Editing failed.")));
        Assert.Equal("Attach a photo.", Assert.Single(result).Content);
        Assert.Single(harness.Edits.Sources);
        var input = harness.Requests[1].GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, item => item.GetProperty("type").GetString() == "reasoning");
        var output = Assert.Single(input.Where(item => item.GetProperty("type").GetString() == "function_call_output"));
        Assert.Equal("call_edit", output.GetProperty("call_id").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"prompt\":null}")]
    [InlineData("{\"prompt\":42}")]
    [InlineData("{\"prompt\":\" \"}")]
    public async Task EditImage_RejectsInvalidArguments(string arguments)
    {
        var images = new FakeImages();
        var tools = new OpenAiChatToolsService(images, images);
        var call = ResponseItem.CreateFunctionCallItem("call_edit", "EditImage", BinaryData.FromString(arguments));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken)));
        Assert.Empty(images.Sources);
    }

    [Fact]
    public async Task EditImage_ReturnsSafeErrorOnProviderFailure()
    {
        var images = new FakeImages { EditFailure = new HttpRequestException("Private provider details") };
        var tools = new OpenAiChatToolsService(images, images);
        var call = ResponseItem.CreateFunctionCallItem("call_edit", "EditImage", BinaryData.FromString("{\"prompt\":\"cup\"}"));
        var result = await Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken,
            new ImageToolContext(new BinaryData(ImageTestData.Png), "Attach a photo.", "Editing failed.")));
        Assert.Equal("Editing failed.", Assert.Single(result).Content);
    }

    private static async Task<List<OpenAiResponse>> Collect(IAsyncEnumerable<OpenAiResponse> stream)
    {
        List<OpenAiResponse> results = [];
        await foreach (var result in stream)
            results.Add(result);
        return results;
    }

    private static OpenAiRequest Request(string text, long userId) => new() { MessageText = text, UserId = userId };

    private static OpenAiOptions CreateClientOptions(string endpoint = "https://example.openai.azure.com") => new()
    {
        OpenAiUrl = endpoint,
        OpenAiKey = "test-key",
        OpenAiChatModelName = "gpt-6-astra",
        OpenAiAudioModelName = "test-audio"
    };

    private static string Event(object update)
    {
        var json = JsonSerializer.SerializeToElement(update);
        return $"event: {json.GetProperty("type").GetString()}\ndata: {json.GetRawText()}\n\n";
    }

    private static string TextDelta(string text) => Event(new
    {
        type = "response.output_text.delta", sequence_number = 0, item_id = "msg_test", output_index = 0, content_index = 0, delta = text
    });

    private static string Completed(params object[] output) => Event(new
    {
        type = "response.completed",
        sequence_number = 10,
        response = new { id = "resp_test", @object = "response", created_at = 1_800_000_000, status = "completed", model = "gpt-6-astra", output }
    });

    private static object Message(string text, string phase = "final_answer") => new
    {
        type = "message", id = "msg_test", role = "assistant", status = "completed", phase,
        content = new[] { new { type = "output_text", text, annotations = Array.Empty<object>() } }
    };

    private static object Reasoning() => new
    {
        type = "reasoning", id = "rs_test", summary = Array.Empty<object>(), encrypted_content = "opaque-reasoning"
    };

    private static object FunctionCall(string callId, string prompt) => new
    {
        type = "function_call", id = $"fc_{callId}", call_id = callId, name = "CreateImage",
        arguments = JsonSerializer.Serialize(new { prompt }), status = "completed"
    };

    private sealed class Harness : IDisposable
    {
        private readonly HttpClient _httpClient;
        public List<JsonElement> Requests { get; } = [];
        public List<Uri> RequestUris { get; } = [];
        public StubImageService Images { get; } = new();
        public FakeImages Edits { get; } = new();
        public OpenAiChatService Service { get; }
        public bool BlockResponses { get; init; }
        public TaskCompletionSource ResponseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Harness(params string[] streams)
        {
            var responses = new Queue<string>(streams);
            _httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
            {
                if (request.RequestUri!.Host == "api.telegram.org")
                {
                    Assert.EndsWith("/getMe", request.RequestUri.AbsolutePath);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"ok\":true,\"result\":{\"id\":123456,\"is_bot\":true,\"first_name\":\"Test Bot\",\"username\":\"test_bot\"}}", Encoding.UTF8, "application/json")
                    };
                }

                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                RequestUris.Add(request.RequestUri);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Requests.Add(body.RootElement.Clone());
                ResponseStarted.TrySetResult();
                if (BlockResponses)
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "text/event-stream")
                };
            }));
            var client = new ResponsesClient(new ApiKeyCredential("test-key"), new OpenAIClientOptions
            {
                Endpoint = new Uri("https://example.openai.azure.com/openai/v1"),
                Transport = new HttpClientPipelineTransport(_httpClient)
            });
            Service = new OpenAiChatService(
                new StubTextMessageService("Test prompt at {0}"),
                Options.Create(CreateClientOptions()),
                client,
                new OpenAiChatToolsService(Images, Edits),
                new EmptyGameRepository(),
                new TelegramBotClient("123456:test-key", _httpClient));
        }

        public void Dispose() => _httpClient.Dispose();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class StubTextMessageService(string systemPrompt) : ITextMessageService
    {
        public Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(name == TextMessageService.TextMessageNames.SystemPrompt ? systemPrompt : string.Empty);
    }

    private sealed class StubImageService : IAiImageService
    {
        public List<string> Prompts { get; } = [];
        public Exception? Failure { get; set; }
        public bool ReturnNoOutput { get; set; }
        public bool ReturnEmptyBytes { get; set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public async IAsyncEnumerable<byte[]> CreateImage(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedCancellationToken = cancellationToken;
            Prompts.Add(prompt);
            if (Failure is not null)
                throw Failure;
            if (ReturnNoOutput)
                yield break;
            yield return ReturnEmptyBytes ? [] : prompt == "first" ? new byte[] { 1, 2, 3 } : new byte[] { 4, 5, 6 };
        }
    }

    private sealed class EmptyGameRepository : IGameRepository
    {
        public Task<User[]> GetActiveUsersForChatAsync(long chatId, CancellationToken cancellationToken) => Task.FromResult(Array.Empty<User>());
        public Task CheckUserAsync(long chatId, long userId, string userName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User[]> GetAllUsersForChat(long chatId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetUserByUserIdAndChatIdAsync(long chatId, long userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetUserByNameAsync(long chatId, string userName) => throw new NotSupportedException();
        public Task<Result?> GetTodayResultAsync(long chatId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result?> GetYesterdayResultAsync(long chatId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result?> GetLastPlayedGameAsync(long chatId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveResultAsync(Result result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerCountViewModel[]> GetAllWinnersForMonthAsync(long chatId, DateTime date, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerCountViewModel?> GetWinnerForMonthAsync(long chatId, DateTime date, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long[]> GetGameEnabledChatIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerCountViewModel[]> GetAllWinnersAsync(long chatId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerCountViewModel?> GetYearWinnerByCountAsync(long chatId, int year, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerCountViewModel[]> GetAllWinnersForYearAsync(long chatId, int year, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Sticker[]> GetStickersBySetAsync(string set, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Sticker?> GetImageByNameAsync(string name, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetUserInactiveFlag(long chatId, long userId, bool inactive, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
