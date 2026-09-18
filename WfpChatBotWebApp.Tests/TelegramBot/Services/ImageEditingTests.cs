using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.TelegramBot.Commands;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class ImageEditingTests
{
    [Theory]
    [InlineData("/redraw add a cup", false)]
    [InlineData("/REDRAW@my_bot\nadd a cup", true)]
    public void Parser_AcceptsTextAndCaptions(string text, bool caption)
    {
        var message = PhotoMessage();
        if (caption) message.Caption = text; else message.Text = text;
        var command = Assert.IsType<RedrawCommand>(CommandParser.Parse(message, "my_bot"));
        Assert.Equal("add a cup", command.Param);
        Assert.Same(message, command.SourceMessage);
    }

    [Fact]
    public void Parser_RejectsOtherBotAndEmptyMessages()
    {
        Assert.Null(CommandParser.Parse(new Message { Text = "/redraw@other cup" }, "my_bot"));
        Assert.Null(CommandParser.Parse(new Message { Text = " \n " }));
    }

    [Fact]
    public void Redraw_PrefersAttachedPhotoOtherwiseUsesReply()
    {
        var message = PhotoMessage();
        message.ReplyToMessage = PhotoMessage();
        Assert.Same(message, new RedrawCommand(message).SourceMessage);
        message.Photo = [];
        Assert.Same(message.ReplyToMessage, new RedrawCommand(message).SourceMessage);
        message.ReplyToMessage = null;
        Assert.Null(new RedrawCommand(message).SourceMessage);
    }

    [Fact]
    public void ReferenceImage_EncodesRawBase64AndRejectsUnsupportedOrOversizeInputs()
    {
        Assert.Equal(Convert.ToBase64String(ImageTestData.Png),
            ImageInput.ToReferenceImage(new BinaryData(ImageTestData.Png)));
        Assert.Throws<ArgumentException>(() => ImageInput.ToReferenceImage(new BinaryData(Array.Empty<byte>())));
        Assert.Throws<ArgumentException>(() => ImageInput.ToReferenceImage(BinaryData.FromString("not an image")));
        Assert.Throws<ArgumentException>(() => ImageInput.ToReferenceImage(new BinaryData(new byte[ImageInput.MaxBytes + 1])));
    }

    [Theory]
    [InlineData("cup")]
    [InlineData("(2) cup")]
    [InlineData("(0) cup")]
    [InlineData("(funny) cup")]
    public async Task Redraw_RequestsOneImageAndPreservesEntirePrompt(string prompt)
    {
        var sent = 0;
        using var handler = new ImageHttpHandler(async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/getFile"))
                return ImageTestData.Json(new { ok = true, result = new { file_id = "source", file_unique_id = "unique", file_path = "avatar.jpg" } });
            if (path.EndsWith("/avatar.jpg"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(ImageTestData.Png) };
            Assert.EndsWith("/sendPhoto", path);
            var body = await request.Content!.ReadAsStringAsync(token);
            Assert.Contains("42", body);
            sent++;
            return ImageTestData.Json(new { ok = true, result = new { message_id = 43, date = 0, chat = new { id = 10, type = "private" } } });
        });
        using var client = new HttpClient(handler);
        var images = new FakeImages();
        var message = PhotoMessage();
        message.Caption = $"/redraw {prompt}";
        var command = new RedrawCommand(message);
        var sut = new RedrawCommandHandler(new TelegramBotClient("123456:test-key", client), new FakeImageMessages(), images, NullLogger<RedrawCommandHandler>.Instance);
        await sut.Handle(command, TestContext.Current.CancellationToken);
        Assert.Equal(1, sent);
        Assert.Equal(ImageTestData.Png, Assert.Single(images.Sources).ToArray());
        Assert.Equal(prompt, Assert.Single(images.EditPrompts));
    }

    [Theory]
    [InlineData("cup")]
    [InlineData("(2) cup")]
    [InlineData("(0) cup")]
    [InlineData("(funny) cup")]
    public async Task Draw_RequestsOneImageAndPreservesEntirePrompt(string prompt)
    {
        var sent = 0;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.EndsWith("/sendPhoto", request.RequestUri!.AbsolutePath);
            sent++;
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { message_id = 43, date = 0, chat = new { id = 10, type = "private" } } }));
        });
        using var client = new HttpClient(handler);
        var images = new FakeImages();
        var command = new DrawCommand(new Message { Id = 42, Chat = new Chat { Id = 10 }, Text = $"/draw {prompt}" });
        var sut = new DrawCommandHandler(new TelegramBotClient("123456:test-key", client), new FakeImageMessages(), images, NullLogger<DrawCommandHandler>.Instance);
        await sut.Handle(command, TestContext.Current.CancellationToken);
        Assert.Equal(1, sent);
        Assert.Equal(prompt, Assert.Single(images.CreatePrompts));
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(false, " \n ")]
    [InlineData(true, "")]
    [InlineData(true, " \n ")]
    public async Task ImageCommands_RejectEmptyPrompts(bool redraw, string prompt)
    {
        var sent = 0;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.EndsWith("/sendMessage", request.RequestUri!.AbsolutePath);
            sent++;
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { message_id = 43, date = 0, chat = new { id = 10, type = "private" } } }));
        });
        using var client = new HttpClient(handler);
        var bot = new TelegramBotClient("123456:test-key", client);
        var images = new FakeImages();
        var message = PhotoMessage();
        if (redraw)
        {
            message.Caption = $"/redraw {prompt}";
            await new RedrawCommandHandler(bot, new FakeImageMessages(), images, NullLogger<RedrawCommandHandler>.Instance)
                .Handle(new RedrawCommand(message), TestContext.Current.CancellationToken);
        }
        else
        {
            message.Text = $"/draw {prompt}";
            await new DrawCommandHandler(bot, new FakeImageMessages(), images, NullLogger<DrawCommandHandler>.Instance)
                .Handle(new DrawCommand(message), TestContext.Current.CancellationToken);
        }
        Assert.Equal(1, sent);
        Assert.Empty(images.CreatePrompts);
        Assert.Empty(images.EditPrompts);
    }

    private static Message PhotoMessage() => new()
    {
        Id = 42, Chat = new Chat { Id = 10 }, Caption = "/redraw cup",
        Photo = [new PhotoSize { FileId = "source", FileUniqueId = "unique", Width = 100, Height = 100 }]
    };

    internal static async Task<List<T>> Collect<T>(IAsyncEnumerable<T> items)
    {
        List<T> result = [];
        await foreach (var item in items) result.Add(item);
        return result;
    }
}

internal static class ImageTestData
{
    public static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    public static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json") };
}

internal sealed class ImageHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}

internal sealed class ImageHttpFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class FakeImageMessages : ITextMessageService
{
    public Dictionary<string, string> Values { get; } = [];
    public Task<string> GetMessageByNameAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Values.GetValueOrDefault(name, name));
    }
}

internal sealed class FakeImages : IAiImageService, IAiImageEditService
{
    public List<BinaryData> Sources { get; } = [];
    public List<string> EditPrompts { get; } = [];
    public List<string> CreatePrompts { get; } = [];
    public Exception? EditFailure { get; set; }
    public Exception? CreateFailure { get; set; }
    public bool EmptyResult { get; set; }
    public Action? OnEdit { get; set; }

    public async IAsyncEnumerable<byte[]> EditImage(string prompt, BinaryData sourceImage, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        Sources.Add(sourceImage);
        EditPrompts.Add(prompt);
        OnEdit?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (EditFailure is not null) throw EditFailure;
        if (EmptyResult) yield break;
        yield return ImageTestData.Png;
    }

    public async IAsyncEnumerable<byte[]> CreateImage(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        CreatePrompts.Add(prompt);
        if (CreateFailure is not null) throw CreateFailure;
        if (EmptyResult) yield break;
        yield return ImageTestData.Png;
    }
}
