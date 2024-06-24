using MediatR;
using Microsoft.AspNetCore.Mvc;
using WfpChatBotWebApp.TelegramBot.Jobs;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class JobController(IMediator mediator) : ControllerBase
{
    [HttpPost("{jobName}")]
    public async Task<IActionResult> Post([FromRoute] string jobName, CancellationToken cancellationToken)
    {
        var request = jobName switch
        {
            "daily" => new DailyWinnerJobRequest(),
            _ => throw new NotSupportedException()
        };

        await mediator.Send(request, cancellationToken);
        
        return Accepted();
    }    
}