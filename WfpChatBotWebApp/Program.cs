using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Telegram.Bot;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.TextMessages;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVaultUri"] ?? string.Empty),
    new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(10) });

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>(httpClient =>
    {
        var botToken = builder.Configuration["BotToken"] ?? string.Empty;
        return new TelegramBotClient(new TelegramBotClientOptions(botToken), httpClient);
    });

var connectionString = builder.Configuration["azure-mysql-connectionstring-349a2"];
builder.Services.AddDbContext<AppDbContext>(
    dbContextOptions => dbContextOptions
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)
        ));

builder.Services.AddHttpClient("Google",
    httpClient => { httpClient.BaseAddress = new Uri(builder.Configuration["GoogleSearchUri"] ?? string.Empty); });

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<ConfigureWebhook>();

builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddMediatR(conf => conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddScoped<IGameRepository, GameRepository>();

builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();
builder.Services.AddScoped<ITextMessageService, TextMessageService>();
builder.Services.AddScoped<IStickerService, StickerService>();

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