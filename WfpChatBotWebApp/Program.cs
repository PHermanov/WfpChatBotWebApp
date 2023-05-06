using Azure.Identity;
using Microsoft.Extensions.Logging.ApplicationInsights;
using System.Reflection;
using Telegram.Bot;
using WfpChatBotWebApp.TelegramBot;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddMediatR(conf => conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();

var app = builder.Build();

app.UseCors(corsPolicyBuilder =>
{
    corsPolicyBuilder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader();
});

app.MapControllers();

app.Run();