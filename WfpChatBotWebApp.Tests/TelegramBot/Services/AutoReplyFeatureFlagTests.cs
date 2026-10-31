using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class AutoReplyFeatureFlagTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Routing_CallsAutoReplyOnlyWhenEnabledForChatAndAlwaysMentionsAll(bool enabled)
    {
        var autoReplyCalls = new List<string>();
        long? checkedChatId = null;
        using var handler = new ImageHttpHandler((request, _) =>
        {
            Assert.EndsWith("/getMe", request.RequestUri!.AbsolutePath);
            return Task.FromResult(ImageTestData.Json(new { ok = true, result = new { id = 123456, is_bot = true, first_name = "Bot", username = "test_bot" } }));
        });
        using var client = new HttpClient(handler);
        var repository = TestProxy.Create<IGameRepository>((method, args) =>
        {
            switch (method!.Name)
            {
                case nameof(IGameRepository.CheckUserAsync):
                    return Task.CompletedTask;
                case nameof(IGameRepository.IsAutoReplyEnabledAsync):
                    checkedChatId = Assert.IsType<long>(args![0]);
                    return Task.FromResult(enabled);
                default:
                    throw new InvalidOperationException($"Unexpected repository call {method.Name}");
            }
        });
        var autoReply = TestProxy.Create<IAutoReplyService>((method, _) =>
        {
            autoReplyCalls.Add(method!.Name);
            return Task.CompletedTask;
        });
        var sut = new TelegramBotService(
            TestProxy.Create<IMediator>((_, _) => throw new InvalidOperationException("Unexpected command")),
            repository,
            autoReply,
            new TelegramBotClient("123456:test-key", client),
            TestProxy.Create<IBotReplyService>((_, _) => throw new InvalidOperationException("Unexpected AI reply")),
            TestProxy.Create<IThrottlingService>((_, _) => throw new InvalidOperationException("Unexpected throttling")),
            NullLogger<TelegramBotService>.Instance);

        await sut.HandleUpdateAsync(new Update
        {
            Message = new Message { Chat = new Chat { Id = 10 }, From = new User { Id = 42, FirstName = "Alice" }, Text = "hello @all" }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(10, checkedChatId);
        if (enabled)
            Assert.Equal([nameof(IAutoReplyService.AutoReplyAsync), nameof(IAutoReplyService.MentionAllAsync)], autoReplyCalls);
        else
            Assert.Equal([nameof(IAutoReplyService.MentionAllAsync)], autoReplyCalls);
    }
}
