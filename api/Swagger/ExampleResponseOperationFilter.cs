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
		var isGet = string.Equals(api.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase);
		var route = api.RelativePath ?? string.Empty;
		var controllerName = api.ActionDescriptor?.RouteValues != null && api.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c : null;

		var matchesExtractRoute = string.Equals(route, "extract", StringComparison.OrdinalIgnoreCase);
		var matchesExtractJobRoute = string.Equals(route, "extract-async", StringComparison.OrdinalIgnoreCase);
		var matchesExtractClauseRoute = string.Equals(route, "extract-clause", StringComparison.OrdinalIgnoreCase);
		var matchesHealthcheckRoute = string.Equals(route, "healthcheck", StringComparison.OrdinalIgnoreCase);

		if (isGet && matchesHealthcheckRoute)
		{
			var example = new OpenApiObject
			{
				["status"] = new OpenApiString("ok")
			};

			operation.Responses ??= new OpenApiResponses();
			if (operation.Responses.TryGetValue("200", out var resp))
			{
				// add media type with example and explicit schema
				var media = new OpenApiMediaType { Example = example };
				try
				{
					var modelType = typeof(ClausesExtractor.Api.Models.HealthcheckResponse);
					if (context.SchemaGenerator != null)
					{
						media.Schema = context.SchemaGenerator.GenerateSchema(modelType, context.SchemaRepository);
					}
				}
				catch { }
				resp.Content["application/json"] = media;
			}
		}
		else if (isGet && matchesHealthcheckRoute)
		{
			var example = new OpenApiObject
			{
				["status"] = new OpenApiString("ok")
			};

			operation.Responses ??= new OpenApiResponses();
			if (operation.Responses.TryGetValue("200", out var resp))
			{
				// add media type with example and explicit schema
				var media = new OpenApiMediaType { Example = example };
				try
				{
					var modelType = typeof(ClausesExtractor.Api.Models.ExtractClauseResponse);
					if (context.SchemaGenerator != null)
					{
						media.Schema = context.SchemaGenerator.GenerateSchema(modelType, context.SchemaRepository);
					}
				}
				catch { }
				resp.Content["application/json"] = media;
			}
		}
		else if (isPost && matchesExtractRoute)
		{
			var example = new OpenApiObject
			{
				["url"] = new OpenApiString("https://example.com/archive.zip"),
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
		else if (isPost && matchesExtractJobRoute)
		{
			var example = new OpenApiObject
			{
				["url"] = new OpenApiString("https://example.com/archive.zip"),
				["jobId"] = new OpenApiString("123e4567-e89b-12d3-a456-426614174000")
			};

			operation.Responses ??= new OpenApiResponses();
			if (operation.Responses.TryGetValue("202", out var resp))
			{
				// add media type with example and explicit schema
				var media = new OpenApiMediaType { Example = example };
				try
				{
					var modelType = typeof(ClausesExtractor.Api.Models.ExtractJobResponse);
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
