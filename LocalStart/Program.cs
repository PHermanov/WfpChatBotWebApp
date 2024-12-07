using Microsoft.Extensions.Hosting;

namespace LocalStart;  

public class Program
{
    static Task Main(string[] args)
    {
        Console.WriteLine("Starting...");
        return ApplicationHost.Run(args);
    }

    static IHostBuilder CreateHostBuilder(string[] args)
        => ApplicationHost.CreateHostBuilder(args);
}