namespace ClausesExtractor.Api.Models;

/// <summary>
/// Represents a parsed file from the extracted ZIP archive.
/// </summary>
/// <param name="FileName">The entry name inside the ZIP archive.</param>
/// <param name="Id">The topic id attribute.</param>
/// <param name="Name">The title text (without the number).</param>
/// <param name="Number">The numeric or numbering token extracted from &lt;ph&gt; inside title.</param>
/// <param name="Body">The serialized content of the &lt;body&gt; element.</param>
public record ParsedFile(string FileName, string? Id, string? Name, string? Number, string? Body);

/// <summary>
/// Represents a parsing error for a specific file inside the archive.
/// </summary>
/// <param name="FileName">The entry name for which the error occurred.</param>
/// <param name="Error">The error message.</param>
public record ErrorFile(string FileName, string Error);

/// <summary>
/// Response returned by the extract endpoint.
/// </summary>
/// <param name="Url">The URL originally requested.</param>
/// <param name="Files">List of successfully parsed files.</param>
/// <param name="Errors">Optional list of files that failed to parse.</param>
public record ExtractResponse(string Url, System.Collections.Generic.List<ParsedFile> Files, System.Collections.Generic.List<ErrorFile>? Errors = null);

/// <summary>
/// Request body for the extract endpoint.
/// </summary>
/// <param name="Url">The URL pointing to the ZIP archive to download and parse.</param>
public record ExtractRequest(string Url);

/// <summary>
/// Request body for the extract endpoint.
/// </summary>
/// <param name="Error">Error message</param>
/// <param name="Details">Error details</param>
public record ErrorResponse(string Error, string? Details = null);
