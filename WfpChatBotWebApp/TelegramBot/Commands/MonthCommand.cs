using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class MonthCommand(Message message) : CommandBase(message), IRequest;

public class MonthCommandHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ITextMessageService messageService)
    : IRequestHandler<MonthCommand>
{
    public async Task Handle(MonthCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var winners = await repository.GetAllWinnersForMonthAsync(request.ChatId, DateTime.Today, cancellationToken);

            if (winners.Length == 0)
                return;

            var inactivePlayers = await repository.GetInactivePlayersAsync(request.ChatId, cancellationToken);
            var inactivePlayersIds = inactivePlayers.Select(p => p.UserId).ToHashSet();

            foreach (var inactiveWinner in
                     winners.Where(inactiveWinner => inactivePlayersIds.Contains(inactiveWinner.UserId)))
            {
                var userMissing = await messageService.GetMessageByNameAsync(TextMessageNames.UserMissing, cancellationToken);
                inactiveWinner.UserName += $" : {userMissing}";
            }

            var allMonthWinners = await messageService.GetMessageByNameAsync(TextMessageNames.AllMonthWinners, cancellationToken);

            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: $"{allMonthWinners}{Environment.NewLine}{string.Join(Environment.NewLine, winners.ToList())}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}