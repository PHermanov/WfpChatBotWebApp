using MediatR;
using Microsoft.AspNetCore.Mvc;
using WfpChatBotWebApp.TelegramBot.Jobs;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class JobController(IConfiguration configuration, IMediator mediator) : ControllerBase
{
    [HttpPost("{jobName}")]
    public async Task<IActionResult> Post([FromRoute] string jobName, [FromQuery] string secret,
        CancellationToken cancellationToken)
    {
        if (secret != configuration.GetValue<string>("FunctionsSecret"))
            return Unauthorized();

        var request = jobName switch
        {
            "daily" => new DailyWinnerJobRequest(),
            _ => null
        };

        if (request == null)
            return BadRequest("Unknown job");
        
        await mediator.Send(request, cancellationToken);

        return Accepted();
    }
}