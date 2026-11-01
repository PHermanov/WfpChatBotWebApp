using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.Instagram;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class AiFeatureFlagTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Routing_CallsBotReplyOnlyWhenAiEnabledForChat(bool enabled)
    {
        var replied = false;
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
                case nameof(IGameRepository.IsAiEnabledAsync):
                    checkedChatId = Assert.IsType<long>(args![0]);
                    return Task.FromResult(enabled);
                default:
                    throw new InvalidOperationException($"Unexpected repository call {method.Name}");
            }
        });
        var botReply = TestProxy.Create<IBotReplyService>((method, _) =>
        {
            Assert.Equal(nameof(IBotReplyService.Reply), method!.Name);
            replied = true;
            return Task.CompletedTask;
        });
        var sut = new TelegramBotService(
            TestProxy.Create<IMediator>((_, _) => throw new InvalidOperationException("Unexpected command")),
            repository,
            TestProxy.Create<IAutoReplyService>((_, _) => throw new InvalidOperationException("Unexpected auto reply")),
            new TelegramBotClient("123456:test-key", client),
            botReply,
            TestProxy.Create<IInstagramService>((_, _) => throw new InvalidOperationException("Unexpected Instagram call")),
            TestProxy.Create<IThrottlingService>((_, _) => throw new InvalidOperationException("Unexpected throttling")),
            NullLogger<TelegramBotService>.Instance);

        await sut.HandleUpdateAsync(new Update
        {
            Message = new Message
            {
                Chat = new Chat { Id = 10 }, From = new User { Id = 42, FirstName = "Alice" }, Text = "hello",
                ReplyToMessage = new Message { From = new User { Id = 123456, IsBot = true, FirstName = "Bot", Username = "test_bot" } }
            }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(10, checkedChatId);
        Assert.Equal(enabled, replied);
    }
}
