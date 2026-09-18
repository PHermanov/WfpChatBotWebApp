using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Services;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class WinnerArtworkTests
{
    [Fact]
    public async Task TemplateLoading_PropagatesCancellationBeforeAcquiringSemaphore()
    {
        using var db = new WfpChatBotWebApp.Persistence.AppDbContext();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new TextMessageService(db, cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetMessageByNameAsync("unused-template", cancellation.Token));
    }

    [Theory]
    [InlineData(WinnerPeriod.Month, "September 2026")]
    [InlineData(WinnerPeriod.Year, "2026")]
    public async Task Artwork_EditsAvatarAndUsesCorrectPeriod(WinnerPeriod period, string label)
    {
        var images = new FakeImages();
        var sut = Service(images);
        var avatar = new BinaryData(ImageTestData.Png);
        var result = await sut.CreateAsync("alice", new DateTime(2026, 9, 16), period, avatar, TestContext.Current.CancellationToken);
        Assert.Equal(ImageTestData.Png, result);
        Assert.Same(avatar, Assert.Single(images.Sources));
        Assert.Contains(label, Assert.Single(images.EditPrompts));
        Assert.Contains("\"alice\"", images.EditPrompts[0]);
        Assert.Empty(images.CreatePrompts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Artwork_GeneratesWhenAvatarMissingOrEditingFails(bool hasAvatar)
    {
        var images = new FakeImages { EditFailure = new HttpRequestException("offline") };
        var result = await Service(images).CreateAsync("alice", new DateTime(2026, 9, 16), WinnerPeriod.Month,
            hasAvatar ? new BinaryData(ImageTestData.Png) : null, TestContext.Current.CancellationToken);
        Assert.Equal(ImageTestData.Png, result);
        var prompt = Assert.Single(images.CreatePrompts);
        Assert.Contains("alice", prompt);
        Assert.Contains("cup", prompt);
        Assert.Contains("congratulations", prompt);
        Assert.Contains("September 2026", prompt);
        Assert.Equal(hasAvatar ? 1 : 0, images.EditPrompts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Artwork_ReturnsNullWhenBothOperationsFailOrReturnNothing(bool empty)
    {
        var images = new FakeImages { EmptyResult = empty, EditFailure = empty ? null : new HttpRequestException(), CreateFailure = empty ? null : new HttpRequestException() };
        var result = await Service(images).CreateAsync("alice", DateTime.Today, WinnerPeriod.Year, new BinaryData(ImageTestData.Png), TestContext.Current.CancellationToken);
        Assert.Null(result);
        Assert.Single(images.EditPrompts);
        Assert.Single(images.CreatePrompts);
    }

    [Fact]
    public async Task Artwork_DoesNotGenerateOnCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var images = new FakeImages { OnEdit = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(images).CreateAsync("alice", DateTime.Today,
            WinnerPeriod.Month, new BinaryData(ImageTestData.Png), cancellation.Token));
        Assert.Empty(images.CreatePrompts);
    }

    [Fact]
    public async Task Artwork_MissingEditTemplateStillUsesGeneratedFallback()
    {
        var images = new FakeImages();
        var messages = Templates();
        messages.Values[Messages.MonthWinnerEditPrompt] = string.Empty;
        var sut = new WinnerArtworkService(images, images, messages, NullLogger<WinnerArtworkService>.Instance);
        Assert.NotNull(await sut.CreateAsync("alice", DateTime.Today, WinnerPeriod.Month, new BinaryData(ImageTestData.Png), TestContext.Current.CancellationToken));
        Assert.Empty(images.EditPrompts);
        Assert.Single(images.CreatePrompts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Announcement_PreservesCaptionAndFallsBackToTextWhenNeeded(bool failPhoto)
    {
        var sentText = new List<JsonElement>();
        using var http = new ImageHttpHandler(async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/getUserProfilePhotos"))
                return ImageTestData.Json(new { ok = true, result = new { total_count = 0, photos = Array.Empty<object>() } });
            if (path.EndsWith("/sendPhoto"))
            {
                Assert.True(failPhoto);
                var error = ImageTestData.Json(new { ok = false, error_code = 400, description = "Test delivery failure" });
                error.StatusCode = HttpStatusCode.BadRequest;
                return error;
            }
            Assert.EndsWith("/sendMessage", path);
            sentText.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement.Clone());
            return ImageTestData.Json(new { ok = true, result = new { message_id = 1, date = 0, chat = new { id = 10, type = "private" } } });
        });
        using var client = new HttpClient(http);
        var artwork = TestProxy.Create<IWinnerArtworkService>((_, args) =>
        {
            Assert.Null(args![3]);
            return Task.FromResult<byte[]?>(failPhoto ? ImageTestData.Png : null);
        });
        var sut = new WinnerAnnouncementService(new TelegramBotClient("123456:test-key", client), artwork, NullLogger<WinnerAnnouncementService>.Instance);
        await sut.SendAsync(10, 42, "alice", new DateTime(2026, 9, 16), WinnerPeriod.Month, "Congratulations alice!", ParseMode.Markdown, TestContext.Current.CancellationToken);
        var text = Assert.Single(sentText);
        Assert.Contains("Congratulations alice!", text.GetProperty("text").GetString());
        Assert.Contains("September 2026", text.GetProperty("text").GetString());
        Assert.Equal("Markdown", text.GetProperty("parse_mode").GetString());
    }

    private static WinnerArtworkService Service(FakeImages images) => new(images, images, Templates(), NullLogger<WinnerArtworkService>.Instance);

    private static FakeImageMessages Templates()
    {
        var messages = new FakeImageMessages();
        foreach (var key in new[] { Messages.MonthWinnerEditPrompt, Messages.MonthWinnerCreatePrompt, Messages.YearWinnerEditPrompt, Messages.YearWinnerCreatePrompt })
            messages.Values[key] = "A funny cup and congratulations for {0} in {1}";
        return messages;
    }
}

public class TestProxy : DispatchProxy
{
    public Func<MethodInfo?, object?[]?, object?> Call { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected call");
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod, args);
    public static T Create<T>(Func<MethodInfo?, object?[]?, object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Call = call;
        return proxy;
    }
}
