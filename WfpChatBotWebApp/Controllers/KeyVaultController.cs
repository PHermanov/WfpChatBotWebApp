using Microsoft.AspNetCore.Mvc;
using WfpChatBotWebApp.Secrets;

namespace WfpChatBotWebApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KeyVaultController : ControllerBase
    {
        private readonly IKeyVaultManager _keyVaultManager;

        public KeyVaultController(IKeyVaultManager keyVaultManager)
        {
            _keyVaultManager = keyVaultManager;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                return Ok(await _keyVaultManager.GetSecretAsync("Ping"));
            }
            catch (Exception ex)
            {
                return Problem(ex.ToString());
            }
        }
    }
}