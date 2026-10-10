using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;
using Telegram.Bot;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.Persistence.Entities;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

#pragma warning disable OPENAI001 // Exercise the installed experimental Responses APIs.

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class OpenAiChatServiceTests
{
    private const string ThreadKey = "10_99";

    [Fact]
    public void AddOpenAiClients_RegistersProjectClientAndAgentReferenceFromConfiguration()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["FoundryProjectEndpoint"] = "https://example.services.ai.azure.com/api/projects/test-project",
            ["FoundryAgentName"] = "wfp-agent",
            ["FoundryAgentVersion"] = "7"
        });

        var projectClient = provider.GetRequiredService<AIProjectClient>();
        var agent = provider.GetRequiredService<AgentReference>();

        Assert.Same(projectClient, provider.GetRequiredService<AIProjectClient>());
        Assert.Equal("wfp-agent", agent.Name);
        Assert.Equal("7", agent.Version);
    }

    [Theory]
    [InlineData(null, "wfp-agent", "7", typeof(AIProjectClient))]
    [InlineData("https://example.services.ai.azure.com/api/projects/test-project", null, "7", typeof(AgentReference))]
    [InlineData("https://example.services.ai.azure.com/api/projects/test-project", "wfp-agent", null, typeof(AgentReference))]
    public void AddOpenAiClients_ThrowsClearErrorWhenSettingIsMissing(string? endpoint, string? name, string? version, Type service)
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["FoundryProjectEndpoint"] = endpoint,
            ["FoundryAgentName"] = name,
            ["FoundryAgentVersion"] = version
        });

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService(service));
        Assert.Contains("must be configured", exception.Message);
    }

    [Fact]
    public async Task ProcessMessage_CreatesConversationAndSendsAgentRequestWithoutPerRequestTools()
    {
        using var harness = new Harness(TextDelta("<b>Hello") + TextDelta(" world</b>") + Completed(Message("<b>Hello world</b>")));

        var results = await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Hello", 42)], TestContext.Current.CancellationToken));

        Assert.Equal(["<b>Hello", "<b>Hello world</b>", "<b>Hello world</b>"], results.Select(result => result.Content));
        Assert.Equal([false, false, true], results.Select(result => result.ContentComplete));

        var creation = Assert.Single(harness.Of(RequestKind.CreateConversation));
        Assert.Equal("10", creation.Body.GetProperty("metadata").GetProperty("telegram_chat_id").GetString());
        Assert.Equal(ThreadKey, creation.Body.GetProperty("metadata").GetProperty("telegram_thread_key").GetString());
        var context = Assert.Single(creation.Body.GetProperty("items").EnumerateArray());
        Assert.Equal("system", context.GetProperty("role").GetString());
        var contextText = context.GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("Your identifiers are: UserId: 123456", contextText);
        Assert.Contains("Telegram chat participants are:", contextText);

        var response = Assert.Single(harness.Of(RequestKind.CreateResponse));
        Assert.Equal("/api/projects/test-project/openai/v1/responses", response.Path);
        var agent = response.Body.GetProperty("agent_reference");
        Assert.Equal("agent_reference", agent.GetProperty("type").GetString());
        Assert.Equal("wfp-agent", agent.GetProperty("name").GetString());
        Assert.Equal("7", agent.GetProperty("version").GetString());
        Assert.Equal("conv_1", response.Body.GetProperty("conversation").GetProperty("id").GetString());
        Assert.True(response.Body.GetProperty("stream").GetBoolean());
        Assert.Equal("auto", response.Body.GetProperty("truncation").GetString());
        Assert.False(response.Body.TryGetProperty("tools", out _));
        Assert.False(response.Body.TryGetProperty("tool_choice", out _));
        Assert.False(response.Body.TryGetProperty("model", out _));
        Assert.False(response.Body.TryGetProperty("reasoning", out _));
        Assert.False(response.Body.TryGetProperty("store", out _));
        Assert.False(response.Body.TryGetProperty("previous_response_id", out _));

        var input = Assert.Single(response.Body.GetProperty("input").EnumerateArray());
        Assert.Equal("user", input.GetProperty("role").GetString());
        Assert.StartsWith("Telegram UserId: 42; Time: ", input.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.EndsWith("\nHello", input.GetProperty("content")[0].GetProperty("text").GetString());

        Assert.All(harness.Requests, request => Assert.Equal("Bearer test-token", request.Authorization));
    }

    [Fact]
    public async Task ProcessMessage_ReusesConversationAndSendsOnlyNewItems()
    {
        using var harness = new Harness(Completed(Message("First.")), Completed(Message("Second.")));

        await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Hello", 42)], TestContext.Current.CancellationToken));
        await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Thanks", 84)], TestContext.Current.CancellationToken));

        Assert.Single(harness.Of(RequestKind.CreateConversation));
        var responses = harness.Of(RequestKind.CreateResponse).ToArray();
        Assert.Equal(2, responses.Length);
        Assert.All(responses, response => Assert.Equal("conv_1", response.Body.GetProperty("conversation").GetProperty("id").GetString()));
        var input = Assert.Single(responses[1].Body.GetProperty("input").EnumerateArray());
        Assert.EndsWith("\nThanks", input.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.True(harness.Store.TryGetConversationId(ThreadKey, out var conversationId));
        Assert.Equal("conv_1", conversationId);
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

        await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [request], TestContext.Current.CancellationToken));

        var image = Assert.Single(harness.Of(RequestKind.CreateResponse)).Body.GetProperty("input")[0].GetProperty("content")[1];
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

        await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [referencedMessage, Request("Change it", 42)], TestContext.Current.CancellationToken));

        var input = Assert.Single(harness.Of(RequestKind.CreateResponse)).Body.GetProperty("input");
        Assert.Equal("assistant", input[0].GetProperty("role").GetString());
        Assert.Equal("output_text", input[0].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Contains("My picture", input[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("user", input[1].GetProperty("role").GetString());
        Assert.Contains("123456", input[1].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("input_image", input[1].GetProperty("content")[1].GetProperty("type").GetString());
        Assert.Contains("Change it", input[2].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ProcessMessage_AppendsEveryToolOutputWithoutAnExtraModelTurn()
    {
        using var harness = new Harness(
            Completed(Reasoning(), Message("Here are your pictures.", "commentary"), FunctionCall("call_first", "first"), FunctionCall("call_second", "second")));

        var results = await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Draw two images", 42)], TestContext.Current.CancellationToken));

        Assert.Single(harness.Of(RequestKind.CreateResponse));
        Assert.Equal(["first", "second"], harness.Images.CreatePrompts);
        Assert.Equal([OpenAiContentType.Text, OpenAiContentType.ImageBytes, OpenAiContentType.ImageBytes], results.Select(result => result.ContentType));
        Assert.All(results.Skip(1), result => Assert.Equal(ImageTestData.Png, result.ImageContent));

        var outputs = harness.ToolOutputs();
        Assert.Equal(["call_first", "call_second"], outputs.Select(output => output.CallId));
        Assert.All(outputs, output => Assert.Contains("Image generated", output.Output));
        Assert.All(harness.Of(RequestKind.CreateItems), request => Assert.Contains("/conversations/conv_1/items", request.Path));
    }

    [Fact]
    public async Task ProcessMessage_SurfacesRefusalText()
    {
        using var harness = new Harness(
            Event(new { type = "response.refusal.delta", sequence_number = 1, item_id = "msg_test", output_index = 0, content_index = 0, delta = "Cannot help." }) +
            Completed(new { type = "message", id = "msg_test", role = "assistant", status = "completed", content = new[] { new { type = "refusal", refusal = "Cannot help." } } }));

        var results = await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Hello", 42)], TestContext.Current.CancellationToken));

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
    public async Task ProcessMessage_RejectsUnsuccessfulStreamsClosesSeenCallsAndStartsNewConversation(string kind, string expectedError)
    {
        var terminal = kind switch
        {
            "empty" => Completed(Reasoning()),
            "failed" => Event(new { type = "response.failed", sequence_number = 2, response = new { id = "resp_failed", status = "failed", error = new { code = "server_error", message = "Failed." }, output = Array.Empty<object>() } }),
            "incomplete" => Event(new { type = "response.incomplete", sequence_number = 2, response = new { id = "resp_incomplete", status = "incomplete", incomplete_details = new { reason = "max_output_tokens" }, output = Array.Empty<object>() } }),
            "error" => Event(new { type = "error", sequence_number = 2, code = "invalid_request_error", message = "Bad input.", param = "input" }),
            _ => string.Empty
        };
        using var harness = new Harness(
            TextDelta("Partial") + Event(new { type = "response.output_item.done", sequence_number = 1, output_index = 0, item = FunctionCall("call_partial", "first") }) + terminal,
            Completed(Message("Recovered.")));
        List<OpenAiResponse> results = [];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var result in harness.Service.ProcessMessage(ThreadKey, 10, [Request("Draw", 42)], TestContext.Current.CancellationToken))
                results.Add(result);
        });

        Assert.Contains(expectedError, exception.Message);
        Assert.DoesNotContain(results, result => result.ContentComplete);
        Assert.Empty(harness.Images.CreatePrompts);
        var closed = Assert.Single(harness.ToolOutputs());
        Assert.Equal("call_partial", closed.CallId);
        Assert.DoesNotContain("Image generated", closed.Output);
        Assert.False(harness.Store.TryGetConversationId(ThreadKey, out _));

        await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Retry", 42)], TestContext.Current.CancellationToken));

        Assert.Equal(2, harness.Of(RequestKind.CreateConversation).Count());
        Assert.Equal("conv_2", harness.Of(RequestKind.CreateResponse).Last().Body.GetProperty("conversation").GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("exception")]
    [InlineData("no-output")]
    public async Task ProcessMessage_ClosesToolCallAndResetsThreadWhenToolFails(string failureKind)
    {
        using var harness = new Harness(Completed(Reasoning(), FunctionCall("call_failed", "first")));
        harness.Images.CreateFailure = failureKind == "exception" ? new InvalidOperationException("Provider failed.") : null;
        harness.Images.EmptyResult = failureKind == "no-output";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Draw", 42)], TestContext.Current.CancellationToken)));

        var closed = Assert.Single(harness.ToolOutputs());
        Assert.Equal("call_failed", closed.CallId);
        Assert.DoesNotContain("Image generated", closed.Output);
        Assert.False(harness.Store.TryGetConversationId(ThreadKey, out _));
    }

    [Fact]
    public async Task ProcessMessage_PropagatesCancellationAndResetsThread()
    {
        using var harness = new Harness(Completed(Message("Unused."))) { BlockResponses = true };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var resultTask = Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Hello", 42)], cancellation.Token));
        await harness.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);
        Assert.Empty(harness.Images.CreatePrompts);
        Assert.False(harness.Store.TryGetConversationId(ThreadKey, out _));
    }

    [Fact]
    public async Task ProcessMessage_CreatesOneConversationAndSerializesConcurrentTurns()
    {
        using var harness = new Harness(Completed(Message("First.")), Completed(Message("Second."))) { BlockResponses = true };

        var first = Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("One", 42)], TestContext.Current.CancellationToken));
        var second = Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Two", 84)], TestContext.Current.CancellationToken));
        await harness.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.Single(harness.Of(RequestKind.CreateConversation));
        Assert.Single(harness.Of(RequestKind.CreateResponse));

        harness.ReleaseResponses();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Single(harness.Of(RequestKind.CreateConversation));
        Assert.Equal(2, harness.Of(RequestKind.CreateResponse).Count());
    }

    [Fact]
    public async Task ProcessMessage_AppendsWebSourcesOnlyToFinalText()
    {
        var message = new
        {
            type = "message", id = "msg_test", role = "assistant", status = "completed", phase = "final_answer",
            content = new[]
            {
                new
                {
                    type = "output_text", text = "Answer",
                    annotations = new object[]
                    {
                        new { type = "url_citation", url = "https://example.com/a?x=1&y=2", title = "Title & Co", start_index = 0, end_index = 6 },
                        new { type = "url_citation", url = "https://example.com/a?x=1&y=2", title = "Duplicate", start_index = 0, end_index = 6 }
                    }
                }
            }
        };
        using var harness = new Harness(TextDelta("Answer") + Completed(new { type = "web_search_call", id = "ws_1", status = "completed" }, message));

        var results = await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("News?", 42)], TestContext.Current.CancellationToken));

        Assert.Equal("Answer", results[0].Content);
        Assert.Equal("Answer\n\n🔗 <a href=\"https://example.com/a?x=1&amp;y=2\">Title &amp; Co</a>", results[^1].Content);
        Assert.True(results[^1].ContentComplete);
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
        var images = new FakeImages();
        var tools = new OpenAiChatToolsService(images, images);
        var call = ResponseItem.CreateFunctionCallItem("call_invalid", name, BinaryData.FromString(arguments));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken)));

        Assert.Empty(images.CreatePrompts);
    }

    [Fact]
    public async Task EditImage_UsesRequestSourceAndDoesNotReuseItForLaterRequests()
    {
        var editCall = new { type = "function_call", id = "fc_edit", call_id = "call_edit", name = "EditImage", arguments = "{\"prompt\":\"add a cup\"}", status = "completed" };
        using var harness = new Harness(Completed(Reasoning(), editCall), Completed(editCall));
        var source = new BinaryData(ImageTestData.Png);

        var result = await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Edit", 42)], TestContext.Current.CancellationToken,
            new ImageToolContext(source, "Attach a photo.", "Editing failed.")));

        Assert.Equal(OpenAiContentType.ImageBytes, Assert.Single(result).ContentType);
        Assert.Same(source, Assert.Single(harness.Images.Sources));

        result = await Collect(harness.Service.ProcessMessage(ThreadKey, 10, [Request("Edit again", 42)], TestContext.Current.CancellationToken,
            new ImageToolContext(null, "Attach a photo.", "Editing failed.")));

        Assert.Equal("Attach a photo.", Assert.Single(result).Content);
        Assert.Single(harness.Images.Sources);
        Assert.Equal(2, harness.Of(RequestKind.CreateResponse).Count());
        var outputs = harness.ToolOutputs();
        Assert.Equal(["call_edit", "call_edit"], outputs.Select(output => output.CallId));
        Assert.Contains("Attach a photo.", outputs[1].Output);
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

    [Fact]
    public async Task EditImage_LoadsThreadImageOnlyWithoutRequestSource()
    {
        var images = new FakeImages();
        var tools = new OpenAiChatToolsService(images, images);
        var call = ResponseItem.CreateFunctionCallItem("call_edit", "EditImage", BinaryData.FromString("{\"prompt\":\"cup\"}"));
        var source = new BinaryData(ImageTestData.Png);
        var threadImage = new BinaryData(ImageTestData.Png);
        var loads = 0;

        await Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken,
            new ImageToolContext(source, "Attach a photo.", "Editing failed.", LoadThreadImage)));
        var result = await Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken,
            new ImageToolContext(null, "Attach a photo.", "Editing failed.", LoadThreadImage)));

        Assert.Equal(OpenAiContentType.ImageBytes, Assert.Single(result).ContentType);
        Assert.Equal(1, loads);
        Assert.Collection(images.Sources,
            first => Assert.Same(source, first),
            second => Assert.Same(threadImage, second));

        ValueTask<BinaryData?> LoadThreadImage(CancellationToken _)
        {
            loads++;
            return ValueTask.FromResult<BinaryData?>(threadImage);
        }
    }

    [Fact]
    public async Task EditImage_ReturnsMissingMessageWhenThreadImageCannotBeLoaded()
    {
        var images = new FakeImages();
        var tools = new OpenAiChatToolsService(images, images);
        var call = ResponseItem.CreateFunctionCallItem("call_edit", "EditImage", BinaryData.FromString("{\"prompt\":\"cup\"}"));

        var result = await Collect(tools.GetToolCallOutput(call, TestContext.Current.CancellationToken,
            new ImageToolContext(null, "Attach a photo.", "Editing failed.",
                _ => ValueTask.FromException<BinaryData?>(new HttpRequestException("Private Telegram details")))));

        Assert.Equal("Attach a photo.", Assert.Single(result).Content);
        Assert.Empty(images.Sources);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .Configure<FoundryAgentOptions>(configuration)
            .AddSingleton<TokenCredential>(new FakeTokenCredential())
            .AddOpenAiClients()
            .BuildServiceProvider();
    }

    private static async Task<List<OpenAiResponse>> Collect(IAsyncEnumerable<OpenAiResponse> stream)
    {
        List<OpenAiResponse> results = [];
        await foreach (var result in stream)
            results.Add(result);
        return results;
    }

    private static OpenAiRequest Request(string text, long userId) => new() { MessageText = text, UserId = userId };

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
        response = new { id = "resp_test", @object = "response", created_at = 1_800_000_000, status = "completed", output }
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

    private enum RequestKind
    {
        CreateConversation,
        CreateItems,
        CreateResponse,
        Other
    }

    private sealed record CapturedRequest(RequestKind Kind, string Path, string? Authorization, JsonElement Body);

    private sealed class Harness : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Lock _lock = new();
        private readonly List<CapturedRequest> _requests = [];
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _conversationCount;

        public FakeImages Images { get; } = new();
        public ConversationStore Store { get; } = new(new MemoryCache(new MemoryCacheOptions()));
        public OpenAiChatService Service { get; }
        public bool BlockResponses { get; init; }
        public TaskCompletionSource ResponseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<CapturedRequest> Requests
        {
            get { lock (_lock) return [.. _requests]; }
        }

        public Harness(params string[] streams)
        {
            var responses = new Queue<string>(streams);
            _httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
            {
                if (request.RequestUri!.Host == "api.telegram.org")
                {
                    Assert.EndsWith("/getMe", request.RequestUri.AbsolutePath);
                    return Json("{\"ok\":true,\"result\":{\"id\":123456,\"is_bot\":true,\"first_name\":\"Test Bot\",\"username\":\"test_bot\"}}");
                }

                Assert.Equal(HttpMethod.Post, request.Method);
                var path = request.RequestUri.AbsolutePath;
                var bodyText = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                using var body = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText);
                var kind = path.EndsWith("/items", StringComparison.Ordinal) ? RequestKind.CreateItems
                    : path.EndsWith("/conversations", StringComparison.Ordinal) ? RequestKind.CreateConversation
                    : path.EndsWith("/responses", StringComparison.Ordinal) ? RequestKind.CreateResponse
                    : RequestKind.Other;
                lock (_lock)
                    _requests.Add(new CapturedRequest(kind, path, request.Headers.Authorization?.ToString(), body.RootElement.Clone()));

                switch (kind)
                {
                    case RequestKind.CreateConversation:
                        var id = $"conv_{Interlocked.Increment(ref _conversationCount)}";
                        return Json($"{{\"id\":\"{id}\",\"object\":\"conversation\",\"created_at\":1800000000,\"metadata\":{{}}}}");
                    case RequestKind.CreateItems:
                        return Json("{\"object\":\"list\",\"data\":[],\"first_id\":null,\"last_id\":null,\"has_more\":false}");
                    case RequestKind.CreateResponse:
                        ResponseStarted.TrySetResult();
                        if (BlockResponses)
                            await _release.Task.WaitAsync(cancellationToken);
                        string stream;
                        lock (_lock)
                            stream = responses.Dequeue();
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(stream, Encoding.UTF8, "text/event-stream")
                        };
                    default:
                        throw new InvalidOperationException($"Unexpected request to {path}.");
                }
            }));

            var projectClient = new AIProjectClient(
                endpoint: new Uri("https://example.services.ai.azure.com/api/projects/test-project"),
                tokenProvider: new FakeTokenCredential(),
                options: new AIProjectClientOptions { Transport = new HttpClientPipelineTransport(_httpClient) });

            var repository = TestProxy.Create<IGameRepository>((method, _) => method!.Name == nameof(IGameRepository.GetActiveUsersForChatAsync)
                ? Task.FromResult(Array.Empty<User>())
                : throw new InvalidOperationException($"Unexpected repository call {method.Name}."));

            Service = new OpenAiChatService(
                projectClient,
                new AgentReference("wfp-agent", "7"),
                new OpenAiChatToolsService(Images, Images),
                repository,
                new TelegramBotClient("123456:test-key", _httpClient),
                Store,
                NullLogger<OpenAiChatService>.Instance);
        }

        public IEnumerable<CapturedRequest> Of(RequestKind kind) => Requests.Where(request => request.Kind == kind);

        public List<(string CallId, string Output)> ToolOutputs() => Of(RequestKind.CreateItems)
            .SelectMany(request => request.Body.GetProperty("items").EnumerateArray())
            .Where(item => item.GetProperty("type").GetString() == "function_call_output")
            .Select(item => (item.GetProperty("call_id").GetString()!, item.GetProperty("output").GetString()!))
            .ToList();

        public void ReleaseResponses() => _release.TrySetResult();

        public void Dispose() => _httpClient.Dispose();

        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
