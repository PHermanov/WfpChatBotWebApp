using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi.Models;

#pragma warning disable OPENAI001 // Exercise the installed experimental Responses APIs.

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class ImageRoutingTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Routing_DispatchesRedrawBeforeBotRepliesAndRespectsThrottling(bool caption, bool allowed)
    {
        IRequest? dispatched = null;
        var throttled = false;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.EndsWith("/getMe", request.RequestUri!.AbsolutePath);
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { id = 123456, is_bot = true, first_name = "Bot", username = "test_bot" } }));
        });
        using var client = new HttpClient(handler);
        var mediator = TestProxy.Create<IMediator>((method, args) =>
        {
            Assert.Equal("Send", method!.Name);
            dispatched = Assert.IsAssignableFrom<IRequest>(args![0]);
            return Task.CompletedTask;
        });
        var repository = TestProxy.Create<IGameRepository>((method, _) =>
        {
            Assert.Equal("CheckUserAsync", method!.Name);
            return Task.CompletedTask;
        });
        var throttle = TestProxy.Create<IThrottlingService>((_, args) =>
        {
            throttled = true;
            Assert.Equal("redraw", args![1]);
            return Task.FromResult(allowed);
        });
        var sut = new TelegramBotService(mediator, repository,
            TestProxy.Create<IAutoReplyService>((_, _) => throw new InvalidOperationException("Unexpected auto reply")),
            new TelegramBotClient("123456:test-key", client),
            TestProxy.Create<IBotReplyService>((_, _) => throw new InvalidOperationException("Unexpected AI reply")),
            throttle, NullLogger<TelegramBotService>.Instance);
        var message = new Message
        {
            Chat = new Chat { Id = 10 }, From = new User { Id = 42, FirstName = "Alice" },
            ReplyToMessage = new Message { From = new User { Id = 123456, IsBot = true, FirstName = "Bot", Username = "test_bot" },
                Photo = [new PhotoSize { FileId = "reply", FileUniqueId = "reply", Width = 20, Height = 20 }] }
        };
        if (caption)
        {
            message.Caption = "/redraw@test_bot cup";
            message.Photo = [new PhotoSize { FileId = "current", FileUniqueId = "current", Width = 20, Height = 20 }];
        }
        else message.Text = "/redraw cup";
        await sut.HandleUpdateAsync(new Update { Message = message }, TestContext.Current.CancellationToken);
        Assert.True(throttled);
        if (allowed) Assert.IsType<RedrawCommand>(dispatched); else Assert.Null(dispatched);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BotReply_ResolvesRepliedPhotoWhetherOrNotItIsAlreadyInHistory(bool inHistory)
    {
        ImageToolContext? received = null;
        OpenAiRequest[]? sent = null;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/getFile"))
                return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { file_id = "reply", file_unique_id = "reply", file_path = "reply.png" } }));
            if (path.EndsWith("/reply.png"))
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(ImageTestData.Png) });
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { message_id = 100, date = 0, chat = new { id = 10, type = "private" } } }));
        });
        using var client = new HttpClient(handler);
        var ai = TestProxy.Create<IOpenAiChatService>((_, args) =>
        {
            sent = Assert.IsType<OpenAiRequest[]>(args![2]);
            received = Assert.IsType<ImageToolContext>(args[4]);
            return Empty();
        });
        var conversationStore = new ConversationStore(new MemoryCache(new MemoryCacheOptions()));
        if (inHistory)
            conversationStore.SetConversationId("10_99", "conv_test");
        var logger = new CapturingLogger<BotReplyService>();
        var sut = new BotReplyService(new TelegramBotClient("123456:test-key", client), ai,
            new FakeImageMessages(), conversationStore, logger);
        await sut.Reply(new Message
        {
            Id = 42, Chat = new Chat { Id = 10 }, Text = "@test_bot домалюй пиво замість телефону", From = new User { Id = 42, FirstName = "Alice" },
            ReplyToMessage = new Message { Id = 99, Chat = new Chat { Id = 10 }, Photo = [new PhotoSize { FileId = "reply", FileUniqueId = "reply", Width = 20, Height = 20 }] }
        }, TestContext.Current.CancellationToken);
        Assert.Empty(logger.Errors);
        Assert.NotNull(sent);
        Assert.Equal(inHistory ? 1 : 2, sent.Length);
        Assert.NotNull(received);
        Assert.Equal(ImageTestData.Png, received.SourceImage!.ToArray());
    }

    [Fact]
    public async Task BotReply_UploadsGeneratedImageBytesUnchanged()
    {
        byte[]? uploaded = null;
        using var handler = new ImageHttpHandler(async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (!path.EndsWith("/sendMessage"))
            {
                Assert.EndsWith("/editMessageMedia", path);
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                var image = Assert.Single(multipart.Where(part => part.Headers.ContentDisposition?.FileName?.Trim('"') == "image.png"));
                uploaded = await image.ReadAsByteArrayAsync(token);
            }
            return ImageTestData.Json(new { ok = true, result = new { message_id = 100, date = 0, text = "...", chat = new { id = 10, type = "private" } } });
        });
        using var client = new HttpClient(handler);
        var ai = TestProxy.Create<IOpenAiChatService>((_, _) => ImageResponse());
        var sut = new BotReplyService(new TelegramBotClient("123456:test-key", client), ai,
            new FakeImageMessages(), new ConversationStore(new MemoryCache(new MemoryCacheOptions())), NullLogger<BotReplyService>.Instance);
        await sut.Reply(new Message
        {
            Id = 42, Chat = new Chat { Id = 10 }, Text = "Draw a cup", From = new User { Id = 42, FirstName = "Alice" }
        }, TestContext.Current.CancellationToken);
        Assert.Equal(ImageTestData.Png, uploaded);

        static async IAsyncEnumerable<OpenAiResponse> ImageResponse()
        {
            await Task.CompletedTask;
            yield return new OpenAiResponse { ContentType = OpenAiContentType.ImageBytes, ImageContent = ImageTestData.Png, ContentComplete = true };
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BotReply_MapsUserAndAnswerMessagesToConversationOnlyWhenTurnSucceeds(bool succeeds)
    {
        string? threadKey = null;
        using var handler = new ImageHttpHandler((_, _) =>
            Task.FromResult(ImageTestData.Json(new { ok = true, result = new { message_id = 100, date = 0, text = "...", chat = new { id = 10, type = "private" } } })));
        using var client = new HttpClient(handler);
        var store = new ConversationStore(new MemoryCache(new MemoryCacheOptions()));
        var ai = TestProxy.Create<IOpenAiChatService>((_, args) =>
        {
            threadKey = Assert.IsType<string>(args![0]);
            if (succeeds)
                store.SetConversationId(threadKey, "conv_test");
            return Empty();
        });
        var sut = new BotReplyService(new TelegramBotClient("123456:test-key", client), ai,
            new FakeImageMessages(), store, NullLogger<BotReplyService>.Instance);

        await sut.Reply(new Message
        {
            Id = 42, Chat = new Chat { Id = 10 }, Text = "@test_bot hello", From = new User { Id = 42, FirstName = "Alice" }
        }, TestContext.Current.CancellationToken);

        Assert.Equal("10_42", threadKey);
        Assert.Equal(succeeds, store.TryGetConversationId("10_42", out var userConversation));
        Assert.Equal(succeeds, store.TryGetConversationId("10_100", out var answerConversation));
        if (succeeds)
        {
            Assert.Equal("conv_test", userConversation);
            Assert.Equal("conv_test", answerConversation);
        }
    }

    [Fact]
    public async Task BotReply_EditsLastThreadImageWhenFollowUpHasNoImage()
    {
        ImageToolContext? received = null;
        List<string> requestedFiles = [];
        using var handler = new ImageHttpHandler(async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/getFile"))
            {
                requestedFiles.Add(await request.Content!.ReadAsStringAsync(token));
                return ImageTestData.Json(new { ok = true, result = new { file_id = "thread_photo", file_unique_id = "thread_photo", file_path = "thread.png" } });
            }
            if (path.EndsWith("/thread.png"))
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(ImageTestData.Png) };
            return ImageTestData.Json(new { ok = true, result = new { message_id = 100, date = 0, text = "...", chat = new { id = 10, type = "private" } } });
        });
        using var client = new HttpClient(handler);
        var store = new ConversationStore(new MemoryCache(new MemoryCacheOptions()));
        var ai = TestProxy.Create<IOpenAiChatService>((_, args) =>
        {
            store.SetConversationId(Assert.IsType<string>(args![0]), "conv_test");
            received = Assert.IsType<ImageToolContext>(args[4]);
            return Empty();
        });
        var logger = new CapturingLogger<BotReplyService>();
        var sut = new BotReplyService(new TelegramBotClient("123456:test-key", client), ai,
            new FakeImageMessages(), store, logger);

        await sut.Reply(new Message
        {
            Id = 42, Chat = new Chat { Id = 10 }, Caption = "@test_bot look", From = new User { Id = 42, FirstName = "Alice" },
            Photo = [new PhotoSize { FileId = "thread_photo", FileUniqueId = "thread_photo", Width = 20, Height = 20 }]
        }, TestContext.Current.CancellationToken);

        Assert.True(store.TryGetLastImageFileId("conv_test", out var lastImage));
        Assert.Equal("thread_photo", lastImage);

        // A follow-up replying to the bot's text answer carries no image of its own.
        await sut.Reply(new Message
        {
            Id = 43, Chat = new Chat { Id = 10 }, Text = "@test_bot add a dad", From = new User { Id = 7, FirstName = "Bob" },
            ReplyToMessage = new Message
            {
                Id = 100, Chat = new Chat { Id = 10 }, Text = "...",
                From = new User { Id = 123456, IsBot = true, FirstName = "Bot", Username = "test_bot" }
            }
        }, TestContext.Current.CancellationToken);

        Assert.Empty(logger.Errors);
        Assert.NotNull(received);
        Assert.Null(received.SourceImage);
        Assert.NotNull(received.LoadThreadImage);
        Assert.Single(requestedFiles);

        var images = new FakeImages();
        var call = ResponseItem.CreateFunctionCallItem("call_edit", "EditImage", BinaryData.FromString("{\"prompt\":\"add a dad\"}"));
        var result = await ImageEditingTests.Collect(new OpenAiChatToolsService(images, images)
            .GetToolCallOutput(call, TestContext.Current.CancellationToken, received));

        Assert.Equal(OpenAiContentType.ImageBytes, Assert.Single(result).ContentType);
        Assert.Equal(ImageTestData.Png, Assert.Single(images.Sources).ToArray());
        Assert.Equal(2, requestedFiles.Count);
        Assert.All(requestedFiles, body => Assert.Contains("thread_photo", body));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BotReply_RecordsGeneratedImageAsLastThreadImageOnlyWhenTurnSucceeds(bool succeeds)
    {
        using var handler = new ImageHttpHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(path.EndsWith("/editMessageMedia")
                ? ImageTestData.Json(new { ok = true, result = new { message_id = 100, date = 0, chat = new { id = 10, type = "private" },
                    photo = new[] { new { file_id = "generated", file_unique_id = "generated", width = 1, height = 1 } } } })
                : ImageTestData.Json(new { ok = true, result = new { message_id = 100, date = 0, text = "...", chat = new { id = 10, type = "private" } } }));
        });
        using var client = new HttpClient(handler);
        var store = new ConversationStore(new MemoryCache(new MemoryCacheOptions()));
        var ai = TestProxy.Create<IOpenAiChatService>((_, args) =>
        {
            if (succeeds)
                store.SetConversationId(Assert.IsType<string>(args![0]), "conv_test");
            return ImageResponse();
        });
        var sut = new BotReplyService(new TelegramBotClient("123456:test-key", client), ai,
            new FakeImageMessages(), store, NullLogger<BotReplyService>.Instance);

        await sut.Reply(new Message
        {
            Id = 42, Chat = new Chat { Id = 10 }, Text = "@test_bot draw a cup", From = new User { Id = 42, FirstName = "Alice" }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(succeeds, store.TryGetLastImageFileId("conv_test", out var lastImage));
        if (succeeds)
            Assert.Equal("generated", lastImage);

        static async IAsyncEnumerable<OpenAiResponse> ImageResponse()
        {
            await Task.CompletedTask;
            yield return new OpenAiResponse { ContentType = OpenAiContentType.ImageBytes, ImageContent = ImageTestData.Png, ContentComplete = true };
        }
    }

    private static async IAsyncEnumerable<OpenAiResponse> Empty()
    {
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<Exception> Errors { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (exception is not null)
            Errors.Add(exception);
    }
}
