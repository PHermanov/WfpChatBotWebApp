using MediatR;
using Telegram.Bot;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class HelpCommand : CommandBase, IRequest
{ }

public class HelpCommandHandler : IRequestHandler<HelpCommand>
{
    private readonly ITelegramBotClient _botClient;

    public HelpCommandHandler(ITelegramBotClient botClient)
        => _botClient = botClient;

    public async Task Handle(HelpCommand request, CancellationToken cancellationToken)
    {
        await _botClient.TrySendTextMessageAsync(request.ChatId, "Messages.Help", cancellationToken: cancellationToken);
    }
}