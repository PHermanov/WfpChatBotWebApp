using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class TelegramBotController : ControllerBase
{
    private readonly string _secretToken;
    private readonly ITelegramBotService _telegramBotService;

    public TelegramBotController(IConfiguration configuration, ITelegramBotService telegramBotService)
    {
        _secretToken = configuration.GetValue<string>("SecretToken");
        _telegramBotService = telegramBotService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update, CancellationToken cancellationToken)
    {
        if (!IsValidRequest(HttpContext.Request))
            return Unauthorized("\"X-Telegram-Bot-Api-Secret-Token\" is invalid");

        await _telegramBotService.HandleUpdateAsync(update, cancellationToken);

        return Ok();
    }

    private bool IsValidRequest(HttpRequest request)
        => request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretTokenHeader)
           && string.Equals(secretTokenHeader, _secretToken, StringComparison.OrdinalIgnoreCase);
}