using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class TodayCommand(Message message) : CommandBase(message), IRequest;

public class TodayCommandHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ITextMessageService textMessageService) : IRequestHandler<TodayCommand>
{
    public async Task Handle(TodayCommand request, CancellationToken cancellationToken)
    {
        var todayResult = await repository.GetTodayResultAsync(request.ChatId, cancellationToken);

        if (todayResult != null)
        {
            var todayWinner = await repository.GetUserByUserIdAndChatIdAsync(todayResult.ChatId, todayResult.UserId, cancellationToken);
            if (todayWinner == null)
                return;

            var messageTemplateAlreadySet = await textMessageService.GetMessageByNameAsync(TextMessageNames.TodayWinnerAlreadySet, cancellationToken);

            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: string.Format(messageTemplateAlreadySet, todayWinner.GetUserMention()),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: await textMessageService.GetMessageByNameAsync(TextMessageNames.WinnerNotSetYet, cancellationToken),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);

            var yesterdayResult = await repository.GetYesterdayResultAsync(request.ChatId, cancellationToken);

            if (yesterdayResult != null)
            {
                var yesterdayWinner = await repository.GetUserByUserIdAndChatIdAsync(yesterdayResult.ChatId, yesterdayResult.UserId, cancellationToken);
                if (yesterdayWinner == null)
                    return;

                var messageTemplateYesterdayWinner = await textMessageService.GetMessageByNameAsync(TextMessageNames.YesterdayWinner, cancellationToken);
                await botClient.TrySendTextMessageAsync(
                    chatId:request.ChatId,
                    text: string.Format(messageTemplateYesterdayWinner, yesterdayWinner.GetUserMention()),
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
        }
    }
}