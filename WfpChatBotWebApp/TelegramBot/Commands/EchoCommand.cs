using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class EchoCommand : CommandBase, IRequest
{
    public string Text { get; }

    public EchoCommand(Message message, string defaultText) : base(message)
        => Text = message.GetAllParamText(defaultText);
}

public class EchoCommandHandler : IRequestHandler<EchoCommand>
{
    private readonly ITelegramBotClient _botClient;

    public EchoCommandHandler(ITelegramBotClient botClient)
        => _botClient = botClient;

    public async Task Handle(EchoCommand request, CancellationToken cancellationToken)
    {
        await _botClient.TrySendTextMessageAsync(request.ChatId, request.Text, cancellationToken: cancellationToken);
    }
}