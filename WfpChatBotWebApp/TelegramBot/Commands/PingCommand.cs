using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class PingCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "ping";
}

public class PingCommandHandler(ITelegramBotClient botClient, ILogger<PingCommandHandler> logger) 
    : IRequestHandler<PingCommand>
{
    public async Task Handle(PingCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} executed", nameof(PingCommandHandler));
        await botClient.TrySendTextMessageAsync(request.ChatId, "Pong", logger: logger, cancellationToken: cancellationToken);
    }
}