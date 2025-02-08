using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot;

public class ConfigureWebhook(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<ConfigureWebhook> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting webhook");
        using var scope = serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        var hostAddress = configuration.GetValue<string>("HostAddress");
        var secretToken = configuration.GetValue<string>("SecretToken");

        var url = $"{hostAddress}telegrambot";

        logger.LogInformation("Configuring webhook: {url}, {secretToken}", url, secretToken);
        
        await botClient.SetWebhook(
            url: url,
            allowedUpdates: [UpdateType.Message],
            secretToken: secretToken,
            cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Webhook stopped");
        using var scope = serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        await botClient.DeleteWebhook(cancellationToken: cancellationToken);
    }
}