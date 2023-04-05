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
        public IActionResult Get()
        {
            try
            {
                return Ok(_keyVaultManager.GetSecret("Ping"));
            }
            catch (Exception ex)
            {
                return Problem(ex.ToString());
            }
        }
    }
}