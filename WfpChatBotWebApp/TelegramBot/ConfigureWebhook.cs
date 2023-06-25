using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace WfpChatBotWebApp.TelegramBot;

public class ConfigureWebhook : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public ConfigureWebhook(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        var hostAddress = _configuration.GetValue<string>("HostAddress");
        var secretToken = _configuration.GetValue<string>("SecretToken");

        var url = $"{hostAddress}telegrambot";

        await botClient.SetWebhookAsync(
            url,
            allowedUpdates: new[] { UpdateType.Message },
            secretToken: secretToken,
            cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        await botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
    }
}