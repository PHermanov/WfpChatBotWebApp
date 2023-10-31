using Azure.Identity;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(GameAzureFunctions.Startup))]

namespace GameAzureFunctions;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        //var executionContextOptions = builder.Services.BuildServiceProvider()
         //   .GetService<IOptions<ExecutionContextOptions>>().Value;
        //var appDirectory = executionContextOptions.AppDirectory;

        var config = new ConfigurationBuilder()
            //.SetBasePath(appDirectory)
            //.AddJsonFile(Path.Combine(appDirectory, "settings.json"), optional: true, reloadOnChange: true)
            .AddAzureKeyVault(new Uri("https://wfpbotkeyvault.vault.azure.net/"), new DefaultAzureCredential());
        
        builder.Services.AddHttpClient();
    }
}
