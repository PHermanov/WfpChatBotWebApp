using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class AllCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "all";
}

public class AllCommandHandler(ITelegramBotClient botClient, 
    IGameRepository repository, 
    ITextMessageService messageService,
    ILogger<AllCommandHandler> logger) 
    : IRequestHandler<AllCommand>
{
    public async Task Handle(AllCommand request, CancellationToken cancellationToken)
    {
        var winners = await repository.GetAllWinnersAsync(request.ChatId, cancellationToken);
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

        var msg = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.AllWinners, cancellationToken)
                  + Environment.NewLine
                  + Environment.NewLine
                  + string.Join(Environment.NewLine, winners.ToList());

        await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            text: msg,
            logger: logger,
            cancellationToken: cancellationToken);
    }
}