using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Telegram.Bot;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot;
using WfpChatBotWebApp.TelegramBot.TextMessages;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVaultUri"]),
    new DefaultAzureCredential());

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>(httpClient =>
    {
        var botToken = builder.Configuration["BotToken"];
        return new TelegramBotClient(new TelegramBotClientOptions(botToken), httpClient);
    });

builder.Services.AddDbContext<AppDbContext>(
    dbContextOptions => dbContextOptions
        .UseMySql(builder.Configuration["azure-mysql-connectionstring-349a2"],
            ServerVersion.AutoDetect(builder.Configuration["azure-mysql-connectionstring-349a2"])
    ));

builder.Services.AddHttpClient("Google", httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.Configuration["GoogleSearchUri"]);
});

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<ConfigureWebhook>();

builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddMediatR(conf => conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();
builder.Services.AddScoped<ITextMessageService, TextMessageService>();

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