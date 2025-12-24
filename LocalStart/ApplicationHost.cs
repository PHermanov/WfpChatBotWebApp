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

namespace LocalStart;

public static class ApplicationHost
{
    private static ILocalTelegramBotService? _telegramBotService;

    public static Task Run(string[] args)
    {
        var host = CreateHostBuilder(args)
            .ConfigureAppConfiguration(c => c.AddJsonFile("appSettingsLocal.json"))
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
        serviceCollection.AddScoped<ITikTokService, TikTokService>();
        serviceCollection.AddScoped<IBotReplyService, BotReplyService>();
        serviceCollection.AddScoped<IAudioTranscribeService, AudioTranscribeService>();
        serviceCollection.AddTransient<IAudioProcessor, AudioProcessor>();
        serviceCollection.AddSingleton<IOpenAiService>(sp => new OpenAiService(
            hostBuilderContext.Configuration,
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<ITelegramBotClient>()));
        serviceCollection.AddSingleton<IContextKeysService, ContextKeysService>();
        serviceCollection.AddSingleton<IThrottlingService, ThrottlingService>();
        serviceCollection.AddSingleton<ILocalTelegramBotService, LocalTelegramBotService>();
        serviceCollection.AddSingleton<IRandomNumbersQueueService, RandomNumbersQueueService>();
        serviceCollection.AddScoped<IRandomService, RandomService>();
    }

    private static void OnApplicationStopping()
    {
        _telegramBotService?.Stop();
    }
}