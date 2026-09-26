using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.InternetSearch;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVaultUri"] ?? string.Empty),
    new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(10) });

builder.Logging.ClearProviders();

builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
    options.ConnectionString = builder.Configuration["TelemetryKey"] ?? string.Empty
);

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

builder.Services.AddHttpClient("Random",
    httpClient =>
    {
        httpClient.BaseAddress = new Uri(builder.Configuration["RandomOrgUri"] ?? string.Empty);
    });

builder.Services.AddHttpClient("Flux", client => client.Timeout = TimeSpan.FromMinutes(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddDbContext<AppDbContext>(
    dbContextOptions => dbContextOptions.UseSqlServer(builder.Configuration["azure-sql-connection-string"]));

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<ConfigureWebhook>();
builder.Services.AddControllers();

builder.Services.AddMediatR(conf =>
{
    conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    conf.LicenseKey = builder.Configuration["MediatrLicense"];
});

builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();
builder.Services.AddScoped<ITextMessageService, TextMessageService>();
builder.Services.AddScoped<IReplyMessagesService, ReplyMessagesService>();
builder.Services.AddScoped<IStickerService, StickerService>();
builder.Services.AddScoped<IAutoReplyService, AutoReplyService>();
builder.Services.AddScoped<IBotReplyService, BotReplyService>();
builder.Services.AddScoped<IAudioTranscribeService, AudioTranscribeService>();
builder.Services.AddTransient<IAudioProcessor, AudioProcessor>();
builder.Services.AddOpenAiClients();
builder.Services.AddSingleton<IOpenAiChatToolsService, OpenAiChatToolsService>();
builder.Services.AddSingleton<IOpenAiChatService, OpenAiChatService>();
builder.Services.AddScoped<IInternetSearchService, GoogleSearchService>();
builder.Services.AddSingleton<FluxImageService>();
builder.Services.AddSingleton<IAiImageService>(services => services.GetRequiredService<FluxImageService>());
builder.Services.AddSingleton<IAiImageEditService>(services => services.GetRequiredService<FluxImageService>());
builder.Services.AddScoped<IWinnerArtworkService, WinnerArtworkService>();
builder.Services.AddScoped<IWinnerAnnouncementService, WinnerAnnouncementService>();
builder.Services.AddSingleton<IOpenAiAudioService, OpenAiAudioService>();
builder.Services.AddSingleton<IContextKeysService, ContextKeysService>();
builder.Services.AddSingleton<IThrottlingService, ThrottlingService>();
builder.Services.AddSingleton<IRandomNumbersQueueService, RandomNumbersQueueService>();
builder.Services.AddSingleton<IRandomService, RandomService>();

builder.Services.Configure<OpenAiOptions>(builder.Configuration);
builder.Services.Configure<ThrottlingServiceOptions>(builder.Configuration);

// Message bus
builder.Services.AddSlimMessageBus(mbb =>
        {
            mbb
                .PerMessageScopeEnabled()
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