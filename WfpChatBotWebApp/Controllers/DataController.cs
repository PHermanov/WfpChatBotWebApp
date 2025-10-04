using Microsoft.AspNetCore.Mvc;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class DataController(IGameRepository repository, ILogger<DataController> logger) : ControllerBase
{
    [HttpGet("chats")]
    public async Task<IActionResult> GetAllChats(
        [FromQuery] string secret,
        CancellationToken cancellationToken)
    {
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
}