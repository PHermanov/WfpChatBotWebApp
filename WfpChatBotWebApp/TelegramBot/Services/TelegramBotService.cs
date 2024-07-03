using MediatR;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;

namespace WfpChatBotWebApp.TelegramBot.Services;

public interface ITelegramBotService
{
    Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}

public class TelegramBotService(IMediator mediator, IGameRepository gameRepository) : ITelegramBotService
{
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

                await gameRepository.CheckUserAsync(message.Chat.Id, message.From.Id, userName);

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
                        await mediator.Send(command, cancellationToken);
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
