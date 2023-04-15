using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp;

public class ConfigureWebhook : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigureWebhook> _logger;

    public ConfigureWebhook(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<ConfigureWebhook> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            var hostAddress = _configuration.GetValue<string>("HostAddress");
            var secretToken = _configuration.GetValue<string>("SecretToken");

            var url = $"{hostAddress}telegrambot";

            _logger.LogInformation("Seting webhook {hook}", url);

            await botClient.SetWebhookAsync(
                url,
                allowedUpdates: Array.Empty<UpdateType>(),
                secretToken: secretToken,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw;
        }

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        await botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
    }
}