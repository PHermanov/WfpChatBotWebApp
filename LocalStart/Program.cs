using Microsoft.Extensions.Hosting;

namespace LocalStart;  

public static class Program
{
    public static Task Main(string[] args)
    {
        Console.WriteLine("Starting...");
        return ApplicationHost.Run(args);
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
        => ApplicationHost.CreateHostBuilder(args);
}