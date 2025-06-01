using Microsoft.AspNetCore.Mvc;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("/")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}