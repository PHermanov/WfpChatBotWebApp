using MediatR;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot.Services;

namespace LocalStart;

public interface ILocalTelegramBotService : ITelegramBotService
{
    Task Start();
    void Stop();
}

public class LocalTelegramBotService : ILocalTelegramBotService 
{
    private ITelegramBotClient _telegramBotClient;
    private readonly ITelegramBotService _telegramBotService;

    public LocalTelegramBotService(ITelegramBotClient telegramBotClient, 
        IMediator mediator,
        IGameRepository gameRepository,
        IAutoReplyService autoReplyService,
        IBotReplyService botReplyService,
        ITikTokService tikTokService,
        IAudioTranscribeService audioTranscribeService,
        IThrottlingService throttlingService,
        ILogger<TelegramBotService> logger)
    {
        _telegramBotClient = telegramBotClient;
        _telegramBotService = new TelegramBotService(
            mediator, 
            gameRepository, 
            autoReplyService,
            _telegramBotClient,
            botReplyService,
            tikTokService,
            audioTranscribeService,
            throttlingService,
            logger);
    }

    public async Task Start()
    {
        var me = await _telegramBotClient.GetMeAsync();
        Console.WriteLine($"Bot started {me.Id} : {me.FirstName}");

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message]
        };
        
        _telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions, 
            cancellationToken);
    }
    
    public void Stop()
    {
        _telegramBotClient = null!;
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        if (exception is ApiRequestException apiRequestException)
        {
            Console.WriteLine("API Request Exception");
            Console.WriteLine(apiRequestException.Message);
        }
        else
        {
            Console.WriteLine(exception.Message);
        }

        Console.ForegroundColor = ConsoleColor.Gray;
        return Task.CompletedTask;
    }

    private Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
       return HandleUpdateAsync(update, cancellationToken);        
    }

    public Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        return _telegramBotService.HandleUpdateAsync(update, cancellationToken);
    }
}