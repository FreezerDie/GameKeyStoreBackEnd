using Microsoft.AspNetCore.Mvc;

namespace GameKeyStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        /// <summary>
        /// Simple test endpoint to verify API routing is working
        /// </summary>
        /// <returns>Test response with timestamp</returns>
        [HttpGet]
        public IActionResult GetTest()
        {
            return Ok(new { 
                message = "API routing is working!", 
                timestamp = DateTime.Now,
                status = "success",
                version = "v1.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            });
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        /// <returns>Health status</returns>
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.Now,
                uptime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }
}
