using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Extensions;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.TelegramBot.Jobs;

public class WednesdayJobRequest : IRequest;

public class WednesdayJobHandler(
    ITelegramBotClient botClient,
    IGameRepository repository,
    ISoraService soraService,
    ITextMessageService messageService,
    ILogger<MonthlyWinnerJobRequest> logger)
    : IRequestHandler<WednesdayJobRequest>
{
    private const string PromptTemplate = "«It’s wednesday my dudes» text. wednesday frog. {0} theme. {1}.";

    private readonly string[] _themes = ["mexican", "forest", "beach", "industrial", "traditional", "anime", "fantasy"];

    private readonly string[] _styles = ["photorealistic", "cartoon", "news broadcast"];

    public async Task Handle(WednesdayJobRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Name} at {Now}", nameof(WednesdayJobHandler), DateTime.UtcNow);

        try
        {
            var allChatIds = await repository.GetAllChatsIdsAsync(cancellationToken);
            logger.LogInformation("{Name} For chats: {Chats} ", nameof(WednesdayJobHandler),
                string.Join(',', allChatIds));

            if (allChatIds.Length == 0)
            {
                logger.LogError("{Name}, no chats found", nameof(WednesdayJobHandler));
                return;
            }

            var message =
                await messageService.GetMessageByNameAsync(TextMessageService.TextMessageNames.WednesdayMyDudes,
                    cancellationToken);
            if (string.IsNullOrWhiteSpace(message))
            {
                logger.LogError("{Name}, message template not found", nameof(WednesdayJobHandler));
                return;
            }

            foreach (var chatId in allChatIds)
            {
                var prompt = string.Format(
                    PromptTemplate,
                    _themes[new Random().Next(_themes.Length)],
                    _styles[new Random().Next(_styles.Length)]);

                var stream = await soraService.GetVideo(prompt, 5, cancellationToken);

                if (stream is not null)
                {
                    await botClient.TrySendAnimationAsync(
                        chatId: chatId,
                        video: InputFile.FromStream(stream, "file.mp4"),
                        caption: message,
                        logger: logger,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.TrySendTextMessageAsync(
                        chatId: chatId,
                        text: message,
                        parseMode: ParseMode.Markdown,
                        logger: logger,
                        cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("{Name}, Exception  {e}", nameof(WednesdayJobHandler), e);
        }
    }
}