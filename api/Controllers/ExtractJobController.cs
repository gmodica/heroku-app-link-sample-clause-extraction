using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ClausesExtractor.Api.Models;
using Heroku.Applink;
using Heroku.Applink.Models;
using StackExchange.Redis;

namespace api.Controllers
{
    [ApiController]
    [Route("/extract-async")]
    public class ExtractJobController : ControllerBase
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<ExtractJobController> _logger;

        public ExtractJobController(IHttpClientFactory httpFactory, ILogger<ExtractJobController> logger)
        {
            _httpFactory = httpFactory;
            _logger = logger;
        }

        /// <summary>
        /// Submits a job to download the ZIP at the provided URL, extracts files in-memory and parses each as XML. The job is processed asynchronously. When the job is processed, results are sent to Salesforce via Bulk API.
        /// </summary>
        /// <response code="200">Successful extraction. Returns parsed files and optional errors.</response>
        /// <response code="400">Bad request — missing url, invalid url, or invalid ZIP.</response>
        /// <response code="504">Gateway Timeout — request to remote timed out.</response>
        /// <response code="500">Internal Server Error — extraction or parsing failed.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ExtractJobResponse), StatusCodes.Status202Accepted, "application/json")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest, "application/json")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized, "application/json")]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError, "application/json")]
        public async Task<IActionResult> Post([FromBody] ExtractRequest body)
        {
            Org? org = null;
            try
            {
                org = ApplinkAuth.ParseRequest(Request.Headers.ToDictionary(h => h.Key, h => h.Value.FirstOrDefault()));
            }
            catch (Exception ex)
            {
                return Unauthorized(new ErrorResponse("Could not parse request context from Salesforce call", ex.Message));
            }
            if (org == null) return Unauthorized(new ErrorResponse("Could not get request context from Salesforce call"));

            var url = body?.Url;
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new ErrorResponse("Missing 'url' parameter."));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                return BadRequest(new ErrorResponse("Invalid URL. Only http and https schemes are allowed."));

            // connect to Redis
            ConnectionMultiplexer? redis = null;
            try
            {
                redis = ConnectToRedis();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("Failed to connect to job queue"));
            }

            var subscriber = redis!.GetSubscriber();

            var jobId = Guid.NewGuid().ToString();

            var payload = new {
                JobId = jobId,
                Url = url,
                SalesforceContext = org
            };
            var message = System.Text.Json.JsonSerializer.Serialize(payload);

            await subscriber.PublishAsync(RedisChannel.Literal("jobs"), message);

            return Accepted(new ExtractJobResponse(url, jobId));
        }

        private ConnectionMultiplexer ConnectToRedis()
        {
            string? redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            if (redisUrl == null) throw new InvalidOperationException("REDIS_URL environment variable is not set");

            var uri = new Uri(redisUrl);
            var userInfoParts = uri.UserInfo.Split(':');
            if (userInfoParts.Length != 2) throw new InvalidOperationException("REDIS_URL is not in the expected format ('redis://user:password@host:port')");

            var configurationOptions = new ConfigurationOptions
            {
                EndPoints = { { uri.Host, uri.Port } },
                Password = userInfoParts[1],
                Ssl = true,
            };
            configurationOptions.CertificateValidation += (sender, cert, chain, errors) => true;
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(configurationOptions);

            return redis;
        }

    }
}
