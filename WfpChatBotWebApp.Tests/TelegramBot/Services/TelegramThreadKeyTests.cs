using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

namespace WfpChatBotWebApp.Tests.TelegramBot.Services;

public class TelegramThreadKeyTests
{
    [Fact]
    public void GetThreadKey_UsesMessageThreadId_WhenAvailable()
    {
        var message = new Message
        {
            Id = 20,
            MessageThreadId = 555,
            Chat = new Chat { Id = -1001234 }
        };

        Assert.Equal("-1001234_555", TelegramThreadKey.GetThreadKey(message));
    }

    [Fact]
    public void GetThreadKey_UsesReplyMessageId_WhenNoThreadId()
    {
        var message = new Message
        {
            Id = 20,
            Chat = new Chat { Id = 10 },
            ReplyToMessage = new Message { Id = 99, Chat = new Chat { Id = 10 } }
        };

        Assert.Equal("10_99", TelegramThreadKey.GetThreadKey(message));
    }

    [Fact]
    public void GetThreadKey_UsesMessageId_WhenNoReplyAndNoThread()
    {
        var message = new Message
        {
            Id = 20,
            Chat = new Chat { Id = 10 }
        };

        Assert.Equal("10_20", TelegramThreadKey.GetThreadKey(message));
    }

    [Fact]
    public void GetThreadKey_RootMessageAndLaterThreadRepliesShareKey()
    {
        var root = new Message { Id = 20, Chat = new Chat { Id = -1001234 } };
        var reply = new Message
        {
            Id = 25,
            MessageThreadId = 20,
            Chat = new Chat { Id = -1001234 },
            ReplyToMessage = new Message { Id = 21, MessageThreadId = 20, Chat = new Chat { Id = -1001234 } }
        };

        Assert.Equal(TelegramThreadKey.GetThreadKey(root), TelegramThreadKey.GetThreadKey(reply));
    }
}
