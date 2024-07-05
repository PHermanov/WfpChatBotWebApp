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

public class TelegramBotService(
    IMediator mediator, 
    IGameRepository gameRepository, 
    IAutoReplyService autoReplyService) 
    : ITelegramBotService
{
    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message == null)
            return;

        if (message.From is { IsBot: true })
            return;

        if ((message.Type is MessageType.Text && !string.IsNullOrWhiteSpace(message.Text))
            || message.Type == MessageType.Photo)
        {
            var userName = message.From?.Username;
            var text = message.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = $"{message.From!.FirstName} {message.From.LastName}";
            }

            await gameRepository.CheckUserAsync(message.Chat.Id, message.From!.Id, userName, cancellationToken);

            // if (message.Type == MessageType.Photo && message.Photo?.Length > 0)
            // {
            //     await _autoReplyService.AutoReplyImageAsync(message, cancellationToken);
            // }
            //else if (IsBotMentioned(message))
            //{
            //    await _botReplyService.Reply(_botUserName, message);
            //}
            //else
            if (text.StartsWith('/'))
            {
                var command = CommandParser.Parse(message);
                if (command != null)
                {
                    await mediator.Send(command, cancellationToken);
                }
            }
            // else if(_tikTokService.ContainsTikTokUrl(message))
            // {
            //     await _tikTokService.TryDownloadVideo(message);
            // }
            else if(!string.IsNullOrEmpty(text))
            {
                await autoReplyService.AutoReplyAsync(message, cancellationToken);
                await autoReplyService.AutoMentionAsync(message, cancellationToken);
            }
        }
    }
}