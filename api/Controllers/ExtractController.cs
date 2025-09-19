using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ClausesExtractor.Api.Models;
using Heroku.Applink;
using Heroku.Applink.Models;

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
    /// <response code="413">Payload Too Large — ZIP exceeds configured max size.</response>
    /// <response code="504">Gateway Timeout — request to remote timed out.</response>
    /// <response code="500">Internal Server Error — extraction or parsing failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ExtractResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status504GatewayTimeout, "application/json")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError, "application/json")]
    public async Task<IActionResult> Post([FromBody] ExtractRequest body)
    {
		Org? org = null;
		try
		{
			if (Request.Headers.TryGetValue("X-Request-Context", out var ctxHeader))
			{
				var xClientContextHeaderValue = ctxHeader.FirstOrDefault();
				org = ApplinkAuth.ParseRequest(xClientContextHeaderValue);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not parse X-Request-Context header: {Message}", ex.Message);
			org = null;
		}

		if (org == null) return BadRequest(new ErrorResponse("Could find or parse X-Request-Context header from Salesforce call"));

		_logger.LogInformation("Processing extract request. OrgId={OrgId}, UserId={UserId}, Username={Username}, OrgType={OrgType}, AccessToken={AccessToken}",
			org.Id, org.User.Id, org.User.Username, org.OrgType, org.AccessToken);

		var url = body?.Url;
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new ErrorResponse("Missing 'url' parameter."));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest(new ErrorResponse("Invalid URL. Only http and https schemes are allowed."));

        var client = _httpFactory.CreateClient();

        const long MaxBytes = 50_000_000; // 50 MB
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        HttpResponseMessage resp;
        try
        {
            resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new ErrorResponse("Request timed out."));
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new ErrorResponse("Request failed.", ex.Message));
        }

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode);

        var contentLength = resp.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > MaxBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        // Read into memory (up to MaxBytes)
        var ms = new System.IO.MemoryStream();
        try
        {
            using var responseStream = await resp.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
            {
                total += read;
                if (total > MaxBytes)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new ErrorResponse("Payload too large."));
                }
                ms.Write(buffer, 0, read);
            }
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new ErrorResponse("Request timed out."));
        }

        ms.Position = 0;

        var parsed = new System.Collections.Generic.List<ParsedFile>();
        var errors = new System.Collections.Generic.List<ErrorFile>();

        try
        {
            using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                try
                {
                    using var entryStream = entry.Open();
                    var doc = System.Xml.Linq.XDocument.Load(entryStream);

                    var topic = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "topic");
                    if (topic == null)
                        topic = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "concept");

                    if (topic == null)
                    {
                        errors.Add(new ErrorFile(entry.FullName, "No <topic> or <concept> element found"));
                        continue;
                    }

                    var id = topic.Attribute("id")?.Value;

                    var titleElem = topic.Elements().FirstOrDefault(e => e.Name.LocalName == "title");
                    string? number = null;
                    string? nameValue = null;
                    if (titleElem != null)
                    {
                        var ph = titleElem.Elements().FirstOrDefault(e => e.Name.LocalName == "ph");
                        number = ph?.Value?.Trim();

                        var titleText = titleElem.Value ?? string.Empty;
                        if (!string.IsNullOrEmpty(number) && titleText.StartsWith(number))
                            nameValue = titleText.Substring(number.Length).Trim();
                        else
                            nameValue = string.IsNullOrWhiteSpace(number) ? titleText.Trim() : titleText.Replace(number, "").Trim();
                    }

                    var bodyElem = topic.Elements().FirstOrDefault(e => e.Name.LocalName == "body");
                    if (bodyElem == null)
                        bodyElem = topic.Elements().FirstOrDefault(e => e.Name.LocalName == "conbody");

                    string? bodyContent = null;
                    if (bodyElem != null)
                        bodyContent = string.Concat(bodyElem.Nodes().Select(n => n.ToString())).Trim();

                    parsed.Add(new ParsedFile(entry.FullName, id, nameValue, number, bodyContent));
                }
                catch (System.Exception ex)
                {
                    errors.Add(new ErrorFile(entry.FullName, ex.Message));
                }
            }

            var response = new ExtractResponse(uri.ToString(), parsed, errors.Count > 0 ? errors : null);
            return Ok(response);
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
