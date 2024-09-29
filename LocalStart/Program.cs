using LocalStart;
using Microsoft.Extensions.Hosting;

class Program
{

    static Task Main(string[] args)
    {
        Console.WriteLine("Starting...");
        return ApplicationHost.Run(args);
    }

    static IHostBuilder CreateHostBuilder(string[] args)
        => ApplicationHost.CreateHostBuilder(args);
}