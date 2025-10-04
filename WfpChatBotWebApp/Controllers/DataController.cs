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
            var users = await repository.GetAllChatsIdsAsync(cancellationToken);

            if (users.Length > 0)
                return Ok(users);
            
            return NotFound();
        }
        catch (Exception e)
        {
            logger.LogError("Exception in {className}: {message}", nameof(DataController), e.Message);
            return Problem();
        }
    }
}