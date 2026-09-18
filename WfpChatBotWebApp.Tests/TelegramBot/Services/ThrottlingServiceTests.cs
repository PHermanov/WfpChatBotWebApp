using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services;
using User = Telegram.Bot.Types.User;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class ThrottlingServiceTests
{
    [Fact]
    public async Task IsAllowed_AlwaysAllowsAndSkipsTelegramCalls_WhenThrottlingDisabled()
    {
        using var handler = new ImageHttpHandler((_, _) => throw new InvalidOperationException("Unexpected Telegram call"));
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ThrottlingService(cache,
            new TelegramBotClient("123456:test-key", client),
            TestProxy.Create<IGameRepository>((_, _) => throw new InvalidOperationException("Unexpected repository call")),
            TestProxy.Create<ITextMessageService>((_, _) => throw new InvalidOperationException("Unexpected message call")),
            Options.Create(new ThrottlingServiceOptions { ThrottlingEnabled = false }),
            NullLogger<ThrottlingService>.Instance);

        Assert.True(await sut.IsAllowed(Message(), "today", TestContext.Current.CancellationToken));
        Assert.True(await sut.IsAllowed(Message(), "today", TestContext.Current.CancellationToken));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task IsAllowed_BlocksRepeatedCommand_WhenThrottlingEnabledByDefault()
    {
        var deleted = 0;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.EndsWith("/deleteMessage", request.RequestUri!.AbsolutePath);
            deleted++;
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = true }));
        });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ThrottlingService(cache,
            new TelegramBotClient("123456:test-key", client),
            TestProxy.Create<IGameRepository>((method, _) =>
            {
                Assert.Equal("GetUserByUserIdAndChatIdAsync", method!.Name);
                return Task.FromResult<WfpChatBotWebApp.Persistence.Entities.User?>(null);
            }),
            TestProxy.Create<ITextMessageService>((_, _) => throw new InvalidOperationException("Unexpected message call")),
            Options.Create(new ThrottlingServiceOptions()),
            NullLogger<ThrottlingService>.Instance);

        Assert.True(await sut.IsAllowed(Message(), "today", TestContext.Current.CancellationToken));
        Assert.False(await sut.IsAllowed(Message(), "today", TestContext.Current.CancellationToken));
        Assert.Equal(1, deleted);
    }

    private static Message Message() => new()
    {
        Chat = new Chat { Id = 10 },
        From = new User { Id = 42, FirstName = "Alice" }
    };
}
