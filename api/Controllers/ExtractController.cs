using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ClausesExtractor.Api.Models;
using Heroku.Applink;
using Heroku.Applink.Models;
using ClausesExtractor.Models;

namespace ClausesExtractor.Api.Controllers;

[ApiController]
[Route("/extract")]
public class ExtractController : ControllerBase
{
	private readonly IHttpClientFactory _httpFactory;
	private readonly ILogger<ExtractController> _logger;

	public ExtractController(IHttpClientFactory httpFactory, ILogger<ExtractController> logger)
	{
		_httpFactory = httpFactory;
		_logger = logger;
	}

	/// <summary>
	/// Downloads the ZIP at the provided URL, extracts files in-memory and parses each as XML to return parsed objects.
	/// </summary>
	/// <response code="200">Successful extraction. Returns parsed files and optional errors.</response>
	/// <response code="400">Bad request — missing url, invalid url, or invalid ZIP.</response>
	/// <response code="504">Gateway Timeout — request to remote timed out.</response>
	/// <response code="500">Internal Server Error — extraction or parsing failed.</response>
	[HttpPost]
	[ProducesResponseType(typeof(ExtractResponse), StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest, "application/json")]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized, "application/json")]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout, "application/json")]
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

		try
		{
			var extractor = new Extractor();
			var results = await extractor.ExtractClauses(uri.ToString());

			var response = new ExtractResponse(uri.ToString(), results.Files, results.Errors);
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
