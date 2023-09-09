using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameAzureFunctions;

public class SelectDayWinnerFunction
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<SecretSettings> _settings;

    public SelectDayWinnerFunction(IOptions<SecretSettings> settings, HttpClient httpClient)
    {
        _settings = settings;
        _httpClient = httpClient;
        //_httpClient
    }

    [FunctionName("SelectDayWinner")]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"{nameof(SelectDayWinnerFunction)} function executed at: {DateTime.Now}");

        log.LogInformation(_settings.Value.HostAddress + "123");
    }
}

