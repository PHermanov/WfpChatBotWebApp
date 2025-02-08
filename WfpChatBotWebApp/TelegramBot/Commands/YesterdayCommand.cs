using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class YesterdayCommand(Message message) : CommandBase(message), IRequest
{
    public override string Name => "yesterday";
}

public class YesterdayCommandHadler(
    ITelegramBotClient botClient, 
    IGameRepository repository, 
    ITextMessageService messageService,
    ILogger<YesterdayCommandHadler> logger) 
    : IRequestHandler<YesterdayCommand>
{
    public async Task Handle(YesterdayCommand request, CancellationToken cancellationToken)
    {
        var yesterdayResult = await repository.GetYesterdayResultAsync(request.ChatId, cancellationToken);

        if (yesterdayResult != null)
        {
            var yesterdayWinner = await repository.GetUserByUserIdAndChatIdAsync(yesterdayResult.ChatId, yesterdayResult.UserId, cancellationToken);
            if (yesterdayWinner == null)
                return;

            var messageTemplate = await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.YesterdayWinner, cancellationToken);
            
            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: string.Format(messageTemplate, yesterdayWinner.GetUserMention()),
                parseMode: ParseMode.Markdown,
                logger: logger,
                cancellationToken: cancellationToken);
        }
    }
}