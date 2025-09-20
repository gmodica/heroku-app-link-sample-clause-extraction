using System.Collections.Generic;
using ClausesExtractor.Models;

namespace ClausesExtractor;

public class Extractor
{
	public async Task<ExtractResults> ExtractClauses(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
			throw new InvalidOperationException("Invalid URL. Only http and https schemes are allowed.");

		using var client = new HttpClient();

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

		HttpResponseMessage resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
		resp.EnsureSuccessStatusCode();

		var contentLength = resp.Content.Headers.ContentLength;

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
				ms.Write(buffer, 0, read);
			}
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException("Request timed out.");
		}

		ms.Position = 0;

		var parsed = new List<ParsedFile>();
		var errors = new List<ErrorFile>();

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

		var response = new ExtractResults(uri.ToString(), parsed, errors.Count > 0 ? errors : null);
		return response;
	}
}
