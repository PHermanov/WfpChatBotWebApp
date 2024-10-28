using Microsoft.AspNetCore.Mvc;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("/")]
public class PingController(ILogger<PingController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        logger.LogInformation("Ping received");
        return Ok();
    }
}