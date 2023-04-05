using Azure.Identity;
using Telegram.Bot;
using WfpChatBotWebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVaultUri"]),
    new DefaultAzureCredential());

builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>(httpClient =>
    {
        var botToken = builder.Configuration.GetValue<string>("BotToken");
        //var botToken =  serviceProvider.GetService<IConfiguration>().GetValue<string>("BotToken");
        TelegramBotClientOptions options = new(botToken);
        return new TelegramBotClient(options, httpClient);
    });

//builder.Services.AddHostedService<ConfigureWebhook>();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();