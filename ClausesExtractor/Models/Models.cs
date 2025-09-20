using System;
using System.Collections.Generic;
using Heroku.Applink.Models;

namespace ClausesExtractor.Models;

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
public record ExtractResults(string Url, List<ParsedFile> Files, List<ErrorFile>? Errors = null);

/// <summary>
/// Response returned by the extract endpoint.
/// </summary>
/// <param name="AccessToken">The access token for Salesforce API.</param>
/// <param name="ApiVersion">The API version to use.</param>
/// <param name="Namespace">The Salesforce namespace.</param>
/// <param name="OrgId">The Salesforce organization Id.</param>
/// <param name="DomainUrl">The Salesforce domain URL.</param>
/// <param name="UserId">The Salesforce user Id.</param>
/// <param name="Username">The Salesforce username.</param>
/// <param name="OrgType">The Salesforce organization type.</param>
public record Context(string AccessToken, string ApiVersion, string Namespace, string OrgId, string DomainUrl, string UserId, string Username, string OrgType);


/// <summary>
/// Response returned by the extract endpoint.
/// </summary>
/// <param name="Url">The URL originally requested.</param>
/// <param name="Files">List of successfully parsed files.</param>
/// <param name="Errors">Optional list of files that failed to parse.</param>
public record ExtractJob(string JobId, string Url, Context SalesforceContext);