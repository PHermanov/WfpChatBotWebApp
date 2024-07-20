using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using WfpChatBotWebApp.TelegramBot;
using WfpChatBotWebApp.TelegramBot.Services;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class TelegramBotController(IConfiguration configuration, ITelegramBotService telegramBotService)
    : ControllerBase
{
    private readonly string _secretToken = configuration.GetValue<string>("SecretToken") ??
                                           throw new NullReferenceException("Secret token not found");

    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Post([FromBody] Update update, CancellationToken cancellationToken)
    {
        if (!IsValidRequest(HttpContext.Request))
            return Unauthorized("\"X-Telegram-Bot-Api-Secret-Token\" is invalid");

        await telegramBotService.HandleUpdateAsync(update, cancellationToken);
        
        // fire and forget
        //_ = Task.Factory.StartNew(async () => await telegramBotService.HandleUpdateAsync(update, cancellationToken), cancellationToken);
        //_ = telegramBotService.HandleUpdateAsync(update, cancellationToken);

        return Ok();
    }

    private bool IsValidRequest(HttpRequest request)
        => request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretTokenHeader)
           && string.Equals(secretTokenHeader, _secretToken, StringComparison.OrdinalIgnoreCase);
}