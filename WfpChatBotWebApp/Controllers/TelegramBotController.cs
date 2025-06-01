using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using SlimMessageBus;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class TelegramBotController(IConfiguration configuration, IMessageBus bus, ILogger<TelegramBotController> logger)
    : ControllerBase
{
    private readonly string _secretToken = configuration.GetValue<string>("SecretToken") ??
                                           throw new NullReferenceException("Secret token not found");

    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<IActionResult> HandleUpdate([FromBody] Update update, CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        logger.LogInformation("Telegram controller received update of type {type}: ", update.Type);
        if (!IsValidRequest(HttpContext.Request))
            return Unauthorized("\"X-Telegram-Bot-Api-Secret-Token\" is invalid");

        // no await to achieve fire-and-forget
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        bus.Publish(update, cancellationToken: cancellationToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        return Ok();
    }

    private bool IsValidRequest(HttpRequest request)
        => request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretTokenHeader)
           && string.Equals(secretTokenHeader, _secretToken, StringComparison.OrdinalIgnoreCase);
}