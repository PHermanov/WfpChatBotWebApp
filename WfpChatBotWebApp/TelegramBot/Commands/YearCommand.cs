using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class YearCommand(Message message) : CommandBase(message), IRequest;

public class YearCommandHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ITextMessageService messageService) : IRequestHandler<YearCommand>
{
    public async Task Handle(YearCommand request, CancellationToken cancellationToken)
    {
        var year = DateTime.Today.Year;

        var winners = await repository.GetAllWinnersForYearAsync(request.ChatId, year, cancellationToken);

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

        var template = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.AllYearWinners, cancellationToken);

        if (string.IsNullOrWhiteSpace(template))
            return;

        var msg = string.Format(template, year)
                  + Environment.NewLine
                  + string.Join(Environment.NewLine, winners.ToList());

        await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            text: msg,
            cancellationToken: cancellationToken);
    }
}