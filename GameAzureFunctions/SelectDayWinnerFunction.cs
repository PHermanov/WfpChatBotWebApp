using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameAzureFunctions;

public class SelectDayWinnerFunction
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public SelectDayWinnerFunction(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    [FunctionName("SelectDayWinner")]
    public void Run([TimerTrigger("0 0 10 * * *")] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"{nameof(SelectDayWinnerFunction)} function executed at: {DateTime.Now}");
        var hostAddress = _configuration.GetValue<string>("HostAddress");
        log.LogInformation(hostAddress + "123");
    }
}

