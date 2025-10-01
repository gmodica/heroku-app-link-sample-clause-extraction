using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ClausesExtractor.Api.Models;
using Heroku.Applink;
using Heroku.Applink.Models;
using ClausesExtractor.Models;

namespace ClausesExtractor.Api.Controllers;

[ApiController]
[Route("extract-clause")]
public class ExtractClauseController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ExtractClauseController> _logger;

    public ExtractClauseController(IHttpClientFactory httpFactory, ILogger<ExtractClauseController> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <summary>
    /// Downloads the ZIP at the provided URL, extracts files in-memory and parses each as XML to return parsed objects.
    /// </summary>
    /// <response code="200">Successful extraction. Returns parsed files and optional errors.</response>
    /// <response code="400">Bad request — missing url, invalid url, or invalid ZIP.</response>
    /// <response code="404">Not found — clause not found</response>
    /// <response code="504">Gateway Timeout — request to remote timed out.</response>
    /// <response code="500">Internal Server Error — extraction or parsing failed.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ExtractClauseResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError, "application/json")]
    public async Task<IActionResult> Get([FromQuery] ExtractClauseRequest body)
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

        var clauseId = body?.Clause;
        if (string.IsNullOrWhiteSpace(clauseId))
            return BadRequest(new ErrorResponse("Missing 'clause' parameter."));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        return BadRequest(new ErrorResponse("Invalid URL. Only http and https schemes are allowed."));

        try
        {
            var extractor = new Extractor();
            var results = await extractor.ExtractClauses(uri.ToString());

            var clause = results.Files.Where(file => file.Id == clauseId).FirstOrDefault();
            if (clause == null) return NotFound();

            var response = new ExtractClauseResponse(clause.Name, clause.Body);

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new ErrorResponse("Request timed out."));
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new ErrorResponse("Request failed.", ex.Message));
        }
        catch (System.IO.InvalidDataException ex)
        {
            return BadRequest(new ErrorResponse("Invalid ZIP file.", ex.Message));
        }
        catch (System.Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: 500, title: "Extraction/Parsing failed");
        }
    }
}
