using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class HelpCommand : CommandBase, IRequest
{
    public HelpCommand(Message message) : base(message)
    { }
}

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