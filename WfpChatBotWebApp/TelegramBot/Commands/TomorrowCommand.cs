using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using Messages = WfpChatBotWebApp.TelegramBot.Services.TextMessageService.TextMessageNames;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class TomorrowCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "tomorrow";
}

public class TomorrowCommandHandler(ITelegramBotClient botClient, ITextMessageService messageService, ILogger<TomorrowCommandHandler> logger)
    : IRequestHandler<TomorrowCommand>
{
    public async Task Handle(TomorrowCommand request, CancellationToken cancellationToken)
    {
        await botClient.TrySendTextMessageAsync(
            chatId: request.ChatId,
            logger: logger,
            parseMode: ParseMode.Html,
            text: await messageService.GetMessageByNameAsync(Messages.Tomorrow, cancellationToken),
            cancellationToken: cancellationToken);
    }
}