using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace WfpChatBotWebApp.Controllers;

public class TelegramBotController : ControllerBase
{
    private readonly string _secretToken;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotController> _logger;

    public TelegramBotController(IConfiguration configuration, ITelegramBotClient botClient, ILogger<TelegramBotController> logger)
    {
        _secretToken = configuration.GetValue<string>("SecretToken");
        _botClient = botClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update, CancellationToken cancellationToken)
    {
        if (!IsValidRequest(HttpContext.Request))
        {
            _logger.LogWarning("Unauthorized");
            return Unauthorized("\"X-Telegram-Bot-Api-Secret-Token\" is invalid");
        }


        if (update.Message is { Text: "/ping" })
        {
            _logger.LogWarning("Ping received");
            await _botClient.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text: "pong",
                cancellationToken: cancellationToken);
        }

        return Ok();
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