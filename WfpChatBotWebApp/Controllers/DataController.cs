using Microsoft.AspNetCore.Mvc;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class DataController(IConfiguration configuration, IGameRepository repository, ILogger<DataController> logger) : ControllerBase
{
    [HttpGet("chats")]
    public async Task<IActionResult> GetAllChats(
    [FromQuery] string secret,
    CancellationToken cancellationToken)
    {
        if (secret != configuration.GetValue<string>("FunctionsSecret"))
            return Unauthorized();

        try
        {
            var chats = await repository.GetAllChatsIdsAsync(cancellationToken);

            if (chats.Length > 0)
                return Ok(chats);

            return NotFound();
        }
        catch (Exception e)
        {
            logger.LogError("Exception in {methodName}: {message}", nameof(GetAllChats), e.Message);
            return Problem();
        }
    }

    [HttpGet("chats/{chatId}/users")]
    public async Task<IActionResult> GetAllUsersForChat(
        [FromQuery] string secret,
        [FromRoute] long chatId,
        CancellationToken cancellationToken)
    {
        if (secret != configuration.GetValue<string>("FunctionsSecret"))
            return Unauthorized();

        try
        {
            var users = await repository.GetAllUsersForChat(chatId, cancellationToken);

            if (users.Length > 0)
                return Ok(users);

            return NotFound();
        }
        catch (Exception e)
        {
            logger.LogError("Exception in {methodName}: {message}", nameof(GetAllUsersForChat), e.Message);
            return Problem();
        }
    }

    [HttpGet("chats/{chatId}/activeusers")]
    public async Task<IActionResult> GetActiveUsersForChat(
    [FromQuery] string secret,
    [FromRoute] long chatId,
    CancellationToken cancellationToken)
    {
        if (secret != configuration.GetValue<string>("FunctionsSecret"))
            return Unauthorized();

        try
        {
            var users = await repository.GetActiveUsersForChatAsync(chatId, cancellationToken);

            if (users.Length > 0)
                return Ok(users);

            return NotFound();
        }
        catch (Exception e)
        {
            logger.LogError("Exception in {methodName}: {message}", nameof(GetActiveUsersForChat), e.Message);
            return Problem();
        }
    }

    [HttpGet("chats/{chatId}/allwinners")]
    public async Task<IActionResult> GetAllWinners(
    [FromQuery] string secret,
    [FromRoute] long chatId,
    CancellationToken cancellationToken)
    {
        if (secret != configuration.GetValue<string>("FunctionsSecret"))
            return Unauthorized();

        try
        {
            var users = await repository.GetAllWinnersAsync(chatId, cancellationToken);

            if (users.Length > 0)
                return Ok(users);

            return NotFound();
        }
        catch (Exception e)
        {
            logger.LogError("Exception in {methodName}: {message}", nameof(GetAllWinners), e.Message);
            return Problem();
        }
    }

    [HttpGet("chats/{chatId}/{date}/monthwinners")]
    public async Task<IActionResult> GetAllWinnersForMonthAsync(
    [FromQuery] string secret,
    [FromRoute] long chatId,
    [FromRoute] DateTime date,
    CancellationToken cancellationToken)
    {
        if (secret != configuration.GetValue<string>("FunctionsSecret"))
            return Unauthorized();

        try
        {
            var users = await repository.GetAllWinnersForMonthAsync(chatId, date, cancellationToken);

            if (users.Length > 0)
                return Ok(users);

            return NotFound();
        }
        catch (Exception e)
        {
            logger.LogError("Exception in {methodName}: {message}", nameof(GetAllWinnersForMonthAsync), e.Message);
            return Problem();
        }
    }
}
