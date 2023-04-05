using Microsoft.AspNetCore.Mvc;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class TelegramBotController : ControllerBase
{
    private readonly string _secretToken;

    public TelegramBotController(IConfiguration configuration)
    {
        _secretToken = configuration.GetValue<string>("SecretToken");
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        if (!IsValidRequest(HttpContext.Request))
            return Unauthorized("\"X-Telegram-Bot-Api-Secret-Token\" is invalid");

        return Accepted();
    }

    [HttpGet]
    public string Test()
    {
        return _secretToken;
    }

    private bool IsValidRequest(HttpRequest request)
        => request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretTokenHeader)
           && string.Equals(secretTokenHeader, _secretToken, StringComparison.OrdinalIgnoreCase);
}