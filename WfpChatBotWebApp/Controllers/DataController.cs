using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WfpChatBotWebApp.Persistence;

namespace WfpChatBotWebApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DataController : ControllerBase
{
    private readonly AppDbContext _appDbContext;

    public DataController(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery]string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name))
        {
            return BadRequest("No Name provided");
        }

        var data = await _appDbContext.TextMessages.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);

        if (data != null)
        {
            return Ok(data);
        }

        return NotFound("Data is not in DB");
    }
}

