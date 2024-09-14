using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot;

public class ConfigureWebhook(IServiceProvider serviceProvider, IConfiguration configuration)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        var hostAddress = configuration.GetValue<string>("HostAddress");
        var secretToken = configuration.GetValue<string>("SecretToken");

        var url = $"{hostAddress}telegrambot";

        await botClient.SetWebhookAsync(
            url: url,
            allowedUpdates: new[] { UpdateType.Message },
            secretToken: secretToken,
            cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        await botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
    }
}