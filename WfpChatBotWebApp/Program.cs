using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot;
using WfpChatBotWebApp.TelegramBot.Services;

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

builder.Services.AddHttpClient("Google",
    httpClient =>
    {
        httpClient.BaseAddress = new Uri(builder.Configuration["GoogleSearchUri"] ?? string.Empty);
    });

builder.Services.AddHttpClient("Pictures",
    httpClient =>
    {
        httpClient.BaseAddress = new Uri(builder.Configuration["PicturesUri"] ?? string.Empty);
    });

var connectionString = builder.Configuration["azure-mysql-connectionstring-349a2"];
builder.Services.AddDbContext<AppDbContext>(
    dbContextOptions => dbContextOptions.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<ConfigureWebhook>();

// Newtonsoft needed for Telegram Bot SDK
builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddMediatR(conf => conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();
builder.Services.AddScoped<ITextMessageService, TextMessageService>();
builder.Services.AddScoped<IReplyMessagesService, ReplyMessagesService>();
builder.Services.AddScoped<IStickerService, StickerService>();
builder.Services.AddScoped<IAutoReplyService, AutoReplyService>();
builder.Services.AddScoped<ITikTokService, TikTokService>();
builder.Services.AddScoped<IBotReplyService, BotReplyService>();
    
builder.Services.AddSingleton<IOpenAiService>(new OpenAiService(builder.Configuration["OpenAiKey"] ?? string.Empty));
builder.Services.AddSingleton<IContextKeysService, ContextKeysService>();

// Message bus
builder.Services.AddSlimMessageBus(mbb =>
        {
            mbb
                .PerMessageScopeEnabled(true)
                .Produce<Update>(x => x.DefaultTopic("telegram-topic"))
                .Consume<Update>(x => x.Topic("telegram-topic")
                    .WithConsumer<ITelegramBotService>(nameof(ITelegramBotService.HandleUpdateAsync))
                    .Instances(10))
                .WithProviderMemory();
        }
    ).AddHttpContextAccessor(); // This is required for the SlimMessageBus.Host.AspNetCore plugin

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

// For integration tests
public partial class Program
{

}