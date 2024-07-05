using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface IAutoReplyService
{
    Task AutoReplyAsync(Message message, CancellationToken cancellationToken);
}

public partial class AutoReplyService(ITelegramBotClient botClient, IReplyMessagesService replyMessagesService)
    : IAutoReplyService
{
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
                        var answers = answer.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        answer = answers[new Random().Next(answers.Length)];
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    await botClient.TrySendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: answer,
                        parseMode: ParseMode.Markdown,
                        replyToMessageId: message.MessageId,
                        cancellationToken: cancellationToken);
                }
            }
        }
    }

    [GeneratedRegex("[^а-яА-Яa-zA-ZіІїЇґҐєЄёЁ]")]
    private static partial Regex MyRegex();
}