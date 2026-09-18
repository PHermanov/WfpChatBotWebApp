using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using WfpChatBotWebApp.Helpers;
using WfpChatBotWebApp.Persistence;
using WfpChatBotWebApp.TelegramBot;
using WfpChatBotWebApp.TelegramBot.Services;
using WfpChatBotWebApp.TelegramBot.Services.InternetSearch;
using WfpChatBotWebApp.TelegramBot.Services.OpenAi;

namespace LocalStart;

public static class ApplicationHost
{
    private static ILocalTelegramBotService? _telegramBotService;

    public static Task Run(string[] args)
    {
        var host = CreateHostBuilder(args)
            .ConfigureLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
                builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            })
            .Build();

        var hostApplicationLifetime = host.Services.GetService<IHostApplicationLifetime>();
        hostApplicationLifetime?.ApplicationStopping.Register(OnApplicationStopping);

        _telegramBotService = host.Services.GetService<ILocalTelegramBotService>();
        _telegramBotService?.Start();

        return host.RunAsync();
    }
    
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(configuration => configuration
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appSettingsLocal.json"), optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args))
            .ConfigureServices(ConfigureServices);
    }
    
    private static void ConfigureServices(
        HostBuilderContext hostBuilderContext,
        IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<AppDbContext>(s =>
        {
            s.UseSqlite("Data Source=local.db");
        });
        
        serviceCollection.AddSingleton<ITelegramBotClient>(_ => 
            new TelegramBotClient(hostBuilderContext.Configuration["BotToken"]!) ?? throw new InvalidOperationException());
        
        serviceCollection.AddHttpClient("Google",
            httpClient =>
            {
                httpClient.BaseAddress = new Uri(hostBuilderContext.Configuration["GoogleSearchUri"] ?? string.Empty);
            });

        serviceCollection.AddHttpClient("Pictures",
            httpClient =>
            {
                httpClient.BaseAddress = new Uri(hostBuilderContext.Configuration["PicturesUri"] ?? string.Empty);
            });
        
        serviceCollection.AddHttpClient("Random",
            httpClient =>
            {
                httpClient.BaseAddress = new Uri(hostBuilderContext.Configuration["RandomOrgUri"] ?? string.Empty);
            });
        
        serviceCollection.AddHttpClient("Flux", client => client.Timeout = TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        serviceCollection.AddMemoryCache();
        serviceCollection.AddMediatR(conf =>
        {
            conf.RegisterServicesFromAssemblyContaining<ConfigureWebhook>();
            conf.LicenseKey = hostBuilderContext.Configuration["MediatrLicense"];
        });
        
        serviceCollection.AddScoped<ITelegramBotService, TelegramBotService>();
        serviceCollection.AddScoped<ITextMessageService, TextMessageService>();
        serviceCollection.AddScoped<IReplyMessagesService, ReplyMessagesService>();
        serviceCollection.AddScoped<IGameRepository, GameRepository>();
        serviceCollection.AddScoped<IStickerService, StickerService>();
        serviceCollection.AddScoped<IAutoReplyService, AutoReplyService>();
        serviceCollection.AddScoped<IBotReplyService, BotReplyService>();
        serviceCollection.AddScoped<IAudioTranscribeService, AudioTranscribeService>();
        serviceCollection.AddTransient<IAudioProcessor, AudioProcessor>();
        serviceCollection.AddSingleton<IOpenAiClientFactory, OpenAiClientFactory>();
        serviceCollection.AddSingleton<IOpenAiChatToolsService, OpenAiChatToolsService>();
        serviceCollection.AddSingleton<IOpenAiChatService, OpenAiChatService>();
        serviceCollection.AddScoped<IInternetSearchService, GoogleSearchService>();
        serviceCollection.AddSingleton<FluxImageService>();
        serviceCollection.AddSingleton<IAiImageService>(services => services.GetRequiredService<FluxImageService>());
        serviceCollection.AddSingleton<IAiImageEditService>(services => services.GetRequiredService<FluxImageService>());
        serviceCollection.AddScoped<IWinnerArtworkService, WinnerArtworkService>();
        serviceCollection.AddScoped<IWinnerAnnouncementService, WinnerAnnouncementService>();
        serviceCollection.AddSingleton<IOpenAiAudioService, OpenAiAudioService>();
        serviceCollection.AddSingleton<IContextKeysService, ContextKeysService>();
        serviceCollection.AddSingleton<IThrottlingService, ThrottlingService>();
        serviceCollection.AddSingleton<ILocalTelegramBotService, LocalTelegramBotService>();
        serviceCollection.AddSingleton<IRandomNumbersQueueService, RandomNumbersQueueService>();
        serviceCollection.AddSingleton<IRandomService, RandomService>();

        serviceCollection.Configure<OpenAiClientFactoryOptions>(hostBuilderContext.Configuration);
        serviceCollection.Configure<OpenAiChatServiceOptions>(hostBuilderContext.Configuration);
        serviceCollection.Configure<ThrottlingServiceOptions>(hostBuilderContext.Configuration);
    }

    private static void OnApplicationStopping()
    {
        _telegramBotService?.Stop();
    }
}