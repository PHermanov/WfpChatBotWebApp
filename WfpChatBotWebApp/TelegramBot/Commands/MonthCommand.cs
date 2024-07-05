using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

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

            var allUsers = await repository.GetAllUsersForChat(request.ChatId, cancellationToken);

            for (var i = 0; i < winners.Length; i++)
            {
                var user = allUsers.FirstOrDefault(u => u.UserId == winners[i].UserId);
                if (user != null)
                {
                    winners[i].UserName = user.UserName ?? string.Empty;

                    if (user.Inactive)
                    {
                        var userMissing = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.UserMissing, cancellationToken);
                        winners[i].UserName += $" : {userMissing}";
                    }
                }
            }

            var allMonthWinners = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.AllMonthWinners, cancellationToken);

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