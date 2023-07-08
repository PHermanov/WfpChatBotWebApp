using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;

namespace WfpChatBotWebApp.TelegramBot;

public interface ITelegramBotService
{
    Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}

public class TelegramBotService : ITelegramBotService
{
    private readonly IMediator _mediator;
    private readonly IGameRepository _gameRepository;

    public TelegramBotService(IMediator mediator, IGameRepository gameRepository)
    {
        _mediator = mediator;
        _gameRepository = gameRepository;
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

                await _gameRepository.CheckUserAsync(message.Chat.Id, message.From.Id, userName);

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
