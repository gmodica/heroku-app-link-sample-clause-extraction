using System.Collections.Generic;
using ClausesExtractor.Models;

namespace ClausesExtractor.Api.Models;

/// <summary>
/// Response returned by the extract endpoint.
/// </summary>
/// <param name="Url">The URL originally requested.</param>
/// <param name="Files">List of successfully parsed files.</param>
/// <param name="Errors">Optional list of files that failed to parse.</param>
public record ExtractResponse(string Url, List<ParsedFile> Files, List<ErrorFile>? Errors = null);

/// <summary>
/// Response returned by the extract async endpoint.
/// </summary>
/// <param name="Url">The URL originally requested.</param>
/// <param name="JobId">The unique identifier for the extraction job.</param>
public record ExtractJobResponse(string Url, string JobId);

/// <summary>
/// Request body for the extract endpoint.
/// </summary>
/// <param name="Url">The URL pointing to the ZIP archive to download and parse.</param>
public record ExtractRequest(string Url);

/// <summary>
/// Request body for the extract endpoint.
/// </summary>
/// <param name="Url">The URL pointing to the ZIP archive to download and parse.</param>
/// <param name="Clause">The clause identifier used to get the clase and extract its contents</param>
public record ExtractClauseRequest(string Url, string Clause);

/// <summary>
/// Response returned by the extract endpoint.
/// </summary>
/// <param name="Subject">The clause subject.</param>
/// <param name="Content">The clause content.</param>
public record ExtractClauseResponse(string? Subject, string? Content);

/// <summary>
/// Error body.
/// </summary>
/// <param name="Error">Error message</param>
/// <param name="Details">Error details</param>
public record ErrorResponse(string Error, string? Details = null);

/// <summary>
/// Response returned by the healthcheck endpoint.
/// </summary>
/// <param name="Status">Status</param>
public record HealthcheckResponse(string Status);
