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
    ITikTokService tikTokService,
    IAudioTranscribeService audioTranscribeService,
    IThrottlingService throttlingService,
    ILogger<TelegramBotService> logger)
    : ITelegramBotService
{
    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message == null)
            return;

        logger.LogInformation("{Name} chat: {ChatId}, Received message {MessageType}", nameof(TelegramBotService), message.Chat.Id, message.Type);

        if (message.From is { IsBot: true })
            return;

        if ((message.Type is MessageType.Text && !string.IsNullOrWhiteSpace(message.Text))
            || message.Type == MessageType.Photo
            || message.Type == MessageType.Voice)
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
                logger.LogError("{Class} bot username is empty", nameof(TelegramBotService));
                return;
            }

            var botMentioned = IsBotMentioned(message, bot.Username);
            if (botMentioned)
            {
                await botReplyService.Reply(bot.Username, message, cancellationToken);
                return;
            }

            if (message.Type == MessageType.Photo && !botMentioned)
                return;

            if (message is { Type: MessageType.Voice, Voice: not null })
            {
                logger.LogInformation("{Name} chat: {ChatId}, Received voice message", nameof(TelegramBotService), message.Chat.Id);
                await audioTranscribeService.Reply(message, cancellationToken);
                return;
            }

            // command received
            if (text.StartsWith('/'))
            {
                var command = CommandParser.Parse(message);
                if (command != null)
                {
                    var allowed = await throttlingService.IsAllowed(message, command.Name, cancellationToken);
                    if (allowed)
                        await mediator.Send((IRequest)command, cancellationToken);
                }
            }
            else if (tikTokService.ContainsTikTokUrl(message))
            {
                await tikTokService.TryDownloadVideo(message, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(text))
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