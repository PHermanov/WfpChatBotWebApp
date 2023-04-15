using Azure.Identity;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Telegram.Bot;
using WfpChatBotWebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVaultUri"]),
    new DefaultAzureCredential());

builder.Logging.AddApplicationInsights(
    configureTelemetryConfiguration: config =>
        config.ConnectionString = builder.Configuration.GetValue<string>("APPLICATIONINSIGHTS"),
    configureApplicationInsightsLoggerOptions: _ => { }
);

builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("Information", LogLevel.Trace);

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

app.MapBotWebhookRoute<TelegramBotClient>(route: "/bot");

app.MapControllers();

app.Run();