using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IAutoReplyService
{
    Task AutoReplyAsync(Message message, CancellationToken cancellationToken);
    Task AutoMentionAsync(Message message, CancellationToken cancellationToken);
}

public partial class AutoReplyService(
    ITelegramBotClient botClient,
    IGameRepository repository,
    IReplyMessagesService replyMessagesService,
    ILogger<AutoReplyService> logger)
    : IAutoReplyService
{
    [GeneratedRegex("[^а-яА-Яa-zA-ZіІїЇґҐєЄёЁ]")]
    private static partial Regex MyRegex();

    public async Task AutoReplyAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text != null)
        {
            var preparedText = MyRegex()
                .Replace(message.Text, string.Empty)
                .ToLower();

            var answer = await replyMessagesService.GetMessageByKeyAsync(preparedText, cancellationToken);

            if (!string.IsNullOrEmpty(answer))
            {
                // replay 70% chance
                if (new Random().Next(1, 101) <= 70)
                {
                    // multiple answers, pick random
                    if (answer.Contains(';'))
                    {
                        var answers = answer.Split([';'], StringSplitOptions.RemoveEmptyEntries);
                        answer = answers[new Random().Next(answers.Length)];
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                    await botClient.TrySendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: answer,
                        parseMode: ParseMode.Markdown,
                        replyToMessageId: message.MessageId,
                        logger: logger,
                        cancellationToken: cancellationToken);
                }
            }
        }
    }

    public async Task AutoMentionAsync(Message message, CancellationToken cancellationToken)
    {
        var splitText = message.Text!.Split([' '], StringSplitOptions.RemoveEmptyEntries);

        if (splitText.Any(w => w.Equals("@all", StringComparison.OrdinalIgnoreCase)))
        {
            var users = (await repository.GetActiveUsersForChatAsync(message.Chat.Id, cancellationToken))
                .Where(user => message.From != null && user.UserId != message.From.Id)
                .ToArray();

            if (users.Length > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                await botClient.TrySendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"\U0001F51D {users.GetUsersMention()}",
                    parseMode: ParseMode.Markdown,
                    replyToMessageId: message.MessageId,
                    logger: logger,
                    cancellationToken: cancellationToken);
            }
        }
    }
}