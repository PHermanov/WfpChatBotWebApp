using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface ITelegramBotService
{
    Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}

public class TelegramBotService(
    IMediator mediator,
    IGameRepository gameRepository,
    IAutoReplyService autoReplyService,
    ITelegramBotClient botClient,
    IBotReplyService botReplyService,
    IThrottlingService throttlingService,
    ILogger<TelegramBotService> logger)
    : ITelegramBotService
{
    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message == null)
            return;

        logger.LogInformation("TelegramBotService: Received message of type {MessageType} in chat: {ChatId},", message.Type, message.Chat.Id);

        if (message.From is { IsBot: true })
            return;

        if ((message.Type is MessageType.Text && !string.IsNullOrWhiteSpace(message.Text))
            || message.Type == MessageType.Photo)
        {
            var userName = message.From?.Username;
            var text = message.Text ?? string.Empty;

            if (message.Type == MessageType.Photo)
                text = message.Caption ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = $"{message.From!.FirstName} {message.From.LastName}";
            }

            await gameRepository.CheckUserAsync(message.Chat.Id, message.From!.Id, userName, cancellationToken);

            var bot = await botClient.GetMe(cancellationToken);
            if (string.IsNullOrEmpty(bot.Username))
            {
                logger.LogError("TelegramBotService: bot username is empty");
                return;
            }

            var command = CommandParser.Parse(message, bot.Username);
            if (command != null)
            {
                if (await throttlingService.IsAllowed(message, command.Name, cancellationToken))
                    await mediator.Send((IRequest)command, cancellationToken);
                return;
            }

            var botMentioned = IsBotMentioned(message, bot.Username);
            if (botMentioned)
            {
                await botReplyService.Reply(message, cancellationToken);
                return;
            }

            if (message.Type == MessageType.Photo && !botMentioned)
                return;

            if (!string.IsNullOrEmpty(text) && !text.TrimStart().StartsWith('/'))
            {
                await autoReplyService.AutoReplyAsync(message, cancellationToken);
                await autoReplyService.AutoMentionAsync(message, cancellationToken);
            }
        }
    }

    private static bool IsBotMentioned(Message message, string botUserName) => message.Type switch
    {
        MessageType.Text => (message.Entities?.Any(e => e.Type is MessageEntityType.Mention) is not null && (message.EntityValues ?? []).Contains($"@{botUserName}"))
                            || message.ReplyToMessage?.From?.Username == botUserName,
        MessageType.Photo when !string.IsNullOrEmpty(message.Caption) => message.Caption.Contains($"@{botUserName}") || message.ReplyToMessage?.From?.Username == botUserName,
        _ => false
    };
}