using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands.Common;

public abstract class CommandWithPrompt(Message message) : CommandWithParam(message)
{
    public string Prompt => string.IsNullOrWhiteSpace(Param)
        ? Message.ReplyToMessage?.Text ?? string.Empty
        : Param;
}