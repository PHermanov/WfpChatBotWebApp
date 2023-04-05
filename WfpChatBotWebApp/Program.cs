using Microsoft.Extensions.Azure;
using WfpChatBotWebApp.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddAzureClients(azureClientFactoryBuilder =>
{
    azureClientFactoryBuilder.AddSecretClient(builder.Configuration.GetSection("KeyVault"));
});

builder.Services.AddSingleton<IKeyVaultManager,KeyVaultManager>();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
