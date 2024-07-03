using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class PingCommand(Message message) : CommandBase(message), IRequest;

public class PingCommandHandler(ITelegramBotClient botClient) : IRequestHandler<PingCommand>
{
    public async Task Handle(PingCommand request, CancellationToken cancellationToken)
    {
        await botClient.TrySendTextMessageAsync(request.ChatId, "Pong", cancellationToken: cancellationToken);
    }
}