using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClausesExtractor.Api.Swagger;

public class ExampleResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
		var api = context.ApiDescription;
		if (api == null) return;

		// Match POST /extract (or controller named Extract)
		var isPost = string.Equals(api.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase);
		var route = api.RelativePath ?? string.Empty;
		var controllerName = api.ActionDescriptor?.RouteValues != null && api.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c : null;

		var matchesExtractRoute = route.Trim('/').StartsWith("extract", StringComparison.OrdinalIgnoreCase) || string.Equals(controllerName, "Extract", StringComparison.OrdinalIgnoreCase);
		if (!isPost || !matchesExtractRoute) return;

		var example = new OpenApiObject
		{
			["url"] = new OpenApiString("https://example.com/archive.zip") ,
			["files"] = new OpenApiArray
			{
				new OpenApiObject
				{
					["fileName"] = new OpenApiString("part_52/52.100.dita"),
					["id"] = new OpenApiString("clause_1"),
					["name"] = new OpenApiString("Sample Clause"),
					["number"] = new OpenApiString("52.100-1"),
					["body"] = new OpenApiString("<p>This is the clause body</p>")
				}
			}
		};

		operation.Responses ??= new OpenApiResponses();
		if (operation.Responses.TryGetValue("200", out var resp))
		{
			resp.Content ??= new Dictionary<string, OpenApiMediaType>();
			if (resp.Content.TryGetValue("application/json", out var existingMedia))
			{
				// preserve existing schema and set example
				existingMedia.Example = example;

				// ensure schema exists for ExtractResponse so it appears under components/schemas
				try
				{
					var modelType = typeof(ClausesExtractor.Api.Models.ExtractResponse);
					if (existingMedia.Schema == null && context.SchemaGenerator != null)
					{
						existingMedia.Schema = context.SchemaGenerator.GenerateSchema(modelType, context.SchemaRepository);
					}
				}
				catch { }
			}
			else
			{
				// add media type with example and explicit schema
				var media = new OpenApiMediaType { Example = example };
				try
				{
					var modelType = typeof(ClausesExtractor.Api.Models.ExtractResponse);
					if (context.SchemaGenerator != null)
					{
						media.Schema = context.SchemaGenerator.GenerateSchema(modelType, context.SchemaRepository);
					}
				}
				catch { }
				resp.Content["application/json"] = media;
			}
		}
    }
}
