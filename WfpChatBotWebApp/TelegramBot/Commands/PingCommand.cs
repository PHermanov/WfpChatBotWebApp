using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class PingCommand : CommandBase, IRequest
{
    public PingCommand(Message message) : base(message)
    { }
}

public class PingCommandHandler : IRequestHandler<PingCommand>
{
    private readonly ITelegramBotClient _botClient;

    public PingCommandHandler(ITelegramBotClient botClient)
        => _botClient = botClient;

    public async Task Handle(PingCommand request, CancellationToken cancellationToken)
    {
        await _botClient.TrySendTextMessageAsync(request.ChatId, "Pong", cancellationToken: cancellationToken);
    }
}