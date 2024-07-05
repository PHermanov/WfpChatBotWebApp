using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Commands.Common;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.TextMessages;

namespace WfpChatBotWebApp.TelegramBot.Commands;

public class MamotaCommand(Message message) : CommandBase(message), IRequest;

public class MamotaCommandHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ITextMessageService messageService,
    IStickerService stickerService)
    : IRequestHandler<MamotaCommand>
{
    public async Task Handle(MamotaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var users = await repository.GetActiveUsersAsync(request.ChatId, cancellationToken);

            if (users.Length == 0)
                return;

            var randomUser = users[new Random().Next(users.Length)];

            var textTemplate = await messageService.GetMessageByNameAsync(TextMessageNames.MamotaSays, cancellationToken);

            await botClient.TrySendTextMessageAsync(
                chatId: request.ChatId,
                text: string.Format(textTemplate, randomUser.GetUserMention()),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);

            var stickerUrl = await stickerService.GetRandomStickerFromSet(StickerService.StickerSet.Mamota, cancellationToken);
            
            if(string.IsNullOrWhiteSpace(stickerUrl))
                return;
            
            await botClient.TrySendStickerAsync(
                chatId: request.ChatId,
                sticker: InputFile.FromUri(stickerUrl),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}