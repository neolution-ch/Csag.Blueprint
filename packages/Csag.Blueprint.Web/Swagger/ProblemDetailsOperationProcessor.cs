namespace Csag.Blueprint.Web.Swagger;

using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

/// <summary>
/// Adds a default error response with <see cref="ProblemDetails"/> schema to every operation,
/// and patches existing error responses (e.g. 401, 403) that lack a body schema.
/// This ensures generated clients (e.g. Orval) type errors as <c>ProblemDetails</c> instead of <c>void</c>.
/// </summary>
public class ProblemDetailsOperationProcessor : IOperationProcessor
{
    /// <inheritdoc/>
    public bool Process(OperationProcessorContext context)
    {
        // Generate adds the schema to the resolver (components/schemas) and returns it.
        var schema = context.SchemaGenerator.Generate(typeof(ProblemDetails), context.SchemaResolver);

        // Wrap in a reference so the spec emits "$ref" instead of inline properties.
        var reference = new JsonSchema { Reference = schema };

        var jsonContent = new OpenApiMediaType { Schema = reference };

        // Add a default error response covering any unhandled status code.
        context.OperationDescription.Operation.Responses["default"] = new OpenApiResponse
        {
            Description = "Error",
            Content = { ["application/problem+json"] = jsonContent },
        };

        // Patch existing error responses that have no body (e.g. 401, 403 added by FastEndpoints).
        // At runtime UseStatusCodePages() wraps these in Problem Details, so the spec should reflect that.
        foreach (var (statusCode, response) in context.OperationDescription.Operation.Responses)
        {
            if (statusCode == "default")
            {
                continue;
            }

            if (!int.TryParse(statusCode, CultureInfo.InvariantCulture, out var code) || code < 400)
            {
                continue;
            }

            if (response.Content.Count == 0)
            {
                response.Content["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = new JsonSchema { Reference = schema },
                };
            }
        }

        return true;
    }
}
