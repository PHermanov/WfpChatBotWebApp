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
    ILogger<TelegramBotService> logger)
    : ITelegramBotService
{
    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message == null)
            return;

        if (message.From is { IsBot: true })
            return;

        if ((message.Type is MessageType.Text && !string.IsNullOrWhiteSpace(message.Text)) 
            || message.Type == MessageType.Photo 
            || message.Type == MessageType.Voice)
        {
            var userName = message.From?.Username;
            var text = message.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = $"{message.From!.FirstName} {message.From.LastName}";
            }

            await gameRepository.CheckUserAsync(message.Chat.Id, message.From!.Id, userName, cancellationToken);

            // if (message.Type == MessageType.Photo && message.Photo?.Length > 0)
            // {
            //     await _autoReplyService.AutoReplyImageAsync(message, cancellationToken);
            // }
            //else 
            var bot = await botClient.GetMeAsync(cancellationToken);
            if (string.IsNullOrEmpty(bot.Username))
            {
                logger.LogError("{Class} bot username is empty", nameof(TelegramBotService));
                return;
            }
            if (message is { Type: MessageType.Voice, Audio: not null })
            {
                await audioTranscribeService.Reply(message, cancellationToken);
            }
            else if (IsBotMentioned(message, bot.Username))
            {
                await botReplyService.Reply(bot.Username, message, cancellationToken);
            }
            // command received
            else if (text.StartsWith('/'))
            {
                var command = CommandParser.Parse(message);
                if (command != null)
                {
                    await mediator.Send(command, cancellationToken);
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

    private static bool IsBotMentioned(Message message, string botUserName) =>
        (message.Entities?.Any(e => e.Type is MessageEntityType.Mention) is not null or false
         && (message.EntityValues ?? Array.Empty<string>()).Contains($"@{botUserName}"))
        || message.ReplyToMessage?.From?.Username == botUserName;
}