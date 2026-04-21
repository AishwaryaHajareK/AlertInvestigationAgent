using Microsoft.AspNetCore.Mvc;

namespace AlertInvestigationAgent.Controllers
{
    [ApiController]
    [Route("api/ping")]
    public class PingController : ControllerBase
    {
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok("Ping successful");
        }


        [HttpGet("fail")]
        public IActionResult Fail()
        {
            return StatusCode(500, "Simulated failure");
        }

    }
}