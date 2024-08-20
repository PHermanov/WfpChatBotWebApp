using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace IntegrationTests;

public class TestServer : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("https_port", "443");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var root = Directory.GetCurrentDirectory();
            var fileProvider = new PhysicalFileProvider(root);
            config.AddJsonFile(fileProvider, "testsettings.json", false, false);
        });

        // builder.ConfigureTestServices(services =>
        // {
        //    var connectionString = builder.GetSetting("azure-mysql-connectionstring-349a2");
        //    services.AddDbContext<AppDbContext>(
        //         dbContextOptions => dbContextOptions.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        //    
        //    services.AddScoped<IGameRepository, GameRepository>();
        // });
    }
}