using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class TomorrowCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "tomorrow";
}

public class TomorrowCommandHandler(ITelegramBotClient botClient, ITextMessageService messageService)
    : IRequestHandler<TomorrowCommand>
{
    public async Task Handle(TomorrowCommand request, CancellationToken cancellationToken)
    {
        await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            text: await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.Tomorrow, cancellationToken),
            cancellationToken: cancellationToken);
    }
}