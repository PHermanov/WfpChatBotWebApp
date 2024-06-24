using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WfpFunctions;

public class DailyWinnerFunction
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public DailyWinnerFunction(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    [FunctionName("DailyWinnerFunction")]

    public void Run([TimerTrigger("0 35 13 * * *")]TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        var hostAddress = _configuration.GetValue<string>("HostAddress");
        log.LogInformation(hostAddress + "123");
    }
}
