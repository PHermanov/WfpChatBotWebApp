using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class JobController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromRoute] string jobName, CancellationToken cancellationToken)
    {
        return Accepted();
    }    
}