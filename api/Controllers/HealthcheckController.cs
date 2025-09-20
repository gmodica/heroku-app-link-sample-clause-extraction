using ClausesExtractor.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers
{
    [ApiController]
    [Route("/healthcheck")]
    public class HealthcheckController : ControllerBase
    {
        /// <summary>
        /// Health check endpoint.
        /// </summary>
        /// <response code="200">Successful extraction. Returns parsed files and optional errors.</response>
        [HttpGet]
        [ProducesResponseType(typeof(HealthcheckResponse), StatusCodes.Status200OK, "application/json")]
        public IActionResult Get()
        {
            return Ok(new HealthcheckResponse("ok"));
        }
    }
}
