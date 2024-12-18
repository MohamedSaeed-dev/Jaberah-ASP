using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jaberah.Controllers
{
    [Route("api")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return Ok(new {message = "Welcome to Jaberah App"});
        }
    }
}
