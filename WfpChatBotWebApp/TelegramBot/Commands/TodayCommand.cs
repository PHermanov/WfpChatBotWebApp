using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class TodayCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "today";
}

public class TodayCommandHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ITextMessageService textMessageService,
    ILogger<TodayCommandHandler> logger) : IRequestHandler<TodayCommand>
{
    public async Task Handle(TodayCommand request, CancellationToken cancellationToken)
    {
        var todayResult = await repository.GetTodayResultAsync(request.ChatId, cancellationToken);

        if (todayResult != null)
        {
            var todayWinner = await repository.GetUserByUserIdAndChatIdAsync(todayResult.ChatId, todayResult.UserId, cancellationToken);
            if (todayWinner == null)
                return;

            var messageTemplateAlreadySet = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.TodayWinnerAlreadySet, cancellationToken);

            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: string.Format(messageTemplateAlreadySet, todayWinner.GetUserMention()),
                parseMode: ParseMode.Markdown,
                logger: logger,
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WinnerNotSetYet, cancellationToken),
                parseMode: ParseMode.Markdown,
                logger: logger,
                cancellationToken: cancellationToken);

            var yesterdayResult = await repository.GetYesterdayResultAsync(request.ChatId, cancellationToken);

            if (yesterdayResult != null)
            {
                var yesterdayWinner = await repository.GetUserByUserIdAndChatIdAsync(yesterdayResult.ChatId, yesterdayResult.UserId, cancellationToken);
                if (yesterdayWinner == null)
                    return;

                var messageTemplateYesterdayWinner = await textMessageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.YesterdayWinner, cancellationToken);
                await botClient.TrySendTextMessageAsync(
                    chatId:request.ChatId,
                    text: string.Format(messageTemplateYesterdayWinner, yesterdayWinner.GetUserMention()),
                    parseMode: ParseMode.Markdown,
                    logger: logger,
                    cancellationToken: cancellationToken);
            }
        }
    }
}