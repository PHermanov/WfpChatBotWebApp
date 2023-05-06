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
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        var data = await _appDbContext.TextMessages.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);

        if (data == null)
        {
            return Ok(data);
        }

        return NotFound();
    }
}

