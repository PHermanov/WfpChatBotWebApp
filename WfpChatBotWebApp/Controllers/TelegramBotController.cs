using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class TelegramBotController : ControllerBase
{
    private readonly string _secretToken;
    private readonly ITelegramBotClient _botClient;

    public TelegramBotController(IConfiguration configuration, ITelegramBotClient botClient)
    {
        _secretToken = configuration.GetValue<string>("SecretToken");
        _botClient = botClient;
    }

    [HttpPost("bot_webhook")]
    public async Task<IActionResult> Post([FromBody] Update update, CancellationToken cancellationToken)
    {
        if (!IsValidRequest(HttpContext.Request))
            return Unauthorized("\"X-Telegram-Bot-Api-Secret-Token\" is invalid");

        if (update.Message is { Text: "/ping" })
        {
            await _botClient.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text: "pong",
                cancellationToken: cancellationToken);
        }

        return Accepted();
    }

    //[HttpGet]
    //public string Test()
    //{
    //    return _secretToken;
    //}

    private bool IsValidRequest(HttpRequest request)
        => request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretTokenHeader)
           && string.Equals(secretTokenHeader, _secretToken, StringComparison.OrdinalIgnoreCase);
}