using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WfpFunctions;

public class DailyWinnerFunction
{
    private readonly HttpClient _httpClient;

    public DailyWinnerFunction(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configuration.GetValue<string>("HostAddress"));
    }

    [FunctionName("DailyWinnerFunction")]
    public void Run([TimerTrigger("0 25 12 * * *")]TimerInfo myTimer, ILogger log)
    {
        log.LogInformation("DailyWinner function executed at: {Now}", DateTime.UtcNow);

        var response = _httpClient.PostAsync("job/daily", null).Result;
        
        log.LogInformation("DailyWinner received response: {Code}, {Reason}", response.StatusCode, response.ReasonPhrase);
    }
}
