using Azure.Identity;
using Telegram.Bot;
using WfpChatBotWebApp;
using Microsoft.Extensions.Logging.AzureAppServices;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddAzureWebAppDiagnostics();

// Add services to the container.
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVaultUri"]),
    new DefaultAzureCredential());

builder.Services.Configure<AzureFileLoggerOptions>(options =>
{
    options.FileName = "bot-azure-log";
    options.FileSizeLimit = 50 * 1024;
    options.RetainedFileCountLimit = 10;
});

builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>(httpClient =>
    {
        var botToken = builder.Configuration.GetValue<string>("BotToken");
        return new TelegramBotClient(new TelegramBotClientOptions(botToken), httpClient);
    });

builder.Services.AddHostedService<ConfigureWebhook>();

builder.Services.AddControllers().AddNewtonsoftJson();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();
//app.MapBotWebhookRoute<TelegramBotController>(route: botConfiguration.Route);
app.MapControllers();

app.Run();