using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.TelegramBot.Commands;

namespace WfpChatBotWebApp.TelegramBot;

public interface ITelegramBotService
{
    Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}

public class TelegramBotService : ITelegramBotService
{
    private readonly IMediator _mediator;

    public TelegramBotService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { } message)
        {
            if (message is { From.IsBot: false, Type: MessageType.Text }
                && !string.IsNullOrWhiteSpace(message.Text))
            {
                var userName = message.From.Username;
                var text = message.Text;

                if (string.IsNullOrWhiteSpace(userName))
                {
                    userName = $"{message.From.FirstName} {message.From.LastName}";
                }

                // var newPlayer = await _gameRepository.CheckPlayerAsync(chatId, message.From.Id, userName);

                //if (IsBotMentioned(message))
                //{
                //    await _botReplyService.Reply(_botUserName, message);
                //}
                //else
                if (text.StartsWith(@"/"))
                {
                    var command = CommandParser.Parse(update.Message);
                    if (command != null)
                    {
                        await _mediator.Send(command, cancellationToken);
                    }
                }
                //else
                //{
                //    await _autoReplyService.AutoReplyAsync(message);
                //    await _autoReplyService.AutoMentionAsync(message);
                //}
            }
        }
    }
}
