using System;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace WfpFunctions;

public class DailyWinnerFunction
{
    [FunctionName("DailyWinnerFunction")]
    public void Run([TimerTrigger("0 52 12 * * *")]TimerInfo myTimer, ILogger log)
    {
        log.LogInformation("DailyWinner function executed at: {Now}", DateTime.UtcNow);

        var hostAdress = Environment.GetEnvironmentVariable("HostAddress", EnvironmentVariableTarget.Process);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(hostAdress)
        };

        var response = httpClient.PostAsync("job/daily", null).Result;
        
        log.LogInformation("DailyWinner received response: {Code}, {Reason}", response.StatusCode, response.ReasonPhrase);

        httpClient.Dispose();
    }
}
