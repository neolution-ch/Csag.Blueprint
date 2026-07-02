namespace Csag.Blueprint.Web.Swagger;

using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

/// <summary>
/// Replaces the auto-generated <c>ProblemDetails</c> and <c>ProblemDetails2</c> schemas with
/// a single unified Problem Details schema. This ensures generated clients get one consistent
/// <c>ProblemDetails</c> error type rather than two separate types from different C# libraries.
/// </summary>
/// <remarks>
/// Runs as a document processor (after all operation processors) so it can rewrite the full
/// document in one pass. The unified schema matches what both ASP.NET Core and FastEndpoints
/// actually produce at runtime.
/// </remarks>
public class UnifiedProblemDetailsDocumentProcessor : IDocumentProcessor
{
    /// <inheritdoc/>
    public void Process(DocumentProcessorContext context)
    {
        var document = context.Document;

        // The ProblemDetails schema must exist (added by ProblemDetailsOperationProcessor).
        if (!document.Definitions.TryGetValue("ProblemDetails", out var pdSchema))
        {
            return;
        }

        // Rebuild the ProblemDetails schema in-place so all existing references remain valid.
        RebuildAsUnifiedSchema(pdSchema, document);

        // Redirect all ProblemDetails2 references to the (now unified) ProblemDetails schema.
        if (document.Definitions.TryGetValue("ProblemDetails2", out var pd2Schema))
        {
            ReplaceSchemaReferences(document, pd2Schema, pdSchema);
            document.Definitions.Remove("ProblemDetails2");
        }
    }

    private static void RebuildAsUnifiedSchema(JsonSchema schema, OpenApiDocument document)
    {
        // Clear auto-generated properties from the C# ProblemDetails class.
        schema.Properties.Clear();

        schema.Type = JsonObjectType.Object;
        schema.Description = "RFC 9457 Problem Details response. Returned by all error responses.";

        // Allow additional properties for extension data (e.g. "exception" in Development).
        schema.AdditionalPropertiesSchema = new JsonSchema { IsNullableRaw = true };

        // Required fields — guaranteed by both ASP.NET Core and FastEndpoints.
        schema.Properties["type"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            IsRequired = true,
        };

        schema.Properties["title"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            IsRequired = true,
        };

        schema.Properties["status"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.Integer,
            Format = "int32",
            IsRequired = true,
        };

        schema.Properties["traceId"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            IsRequired = true,
        };

        // Optional fields — present depending on the error source.
        schema.Properties["detail"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            IsNullableRaw = true,
            Description = "Human-readable explanation specific to this occurrence of the problem.",
        };

        schema.Properties["instance"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            IsNullableRaw = true,
            Description = "A URI reference that identifies the specific occurrence of the problem.",
        };

        // Validation errors array — only present on 400 responses from endpoint validators.
        if (document.Definitions.TryGetValue("ProblemDetails_Error", out var errorItemSchema))
        {
            var errorsProperty = new JsonSchemaProperty
            {
                Type = JsonObjectType.Array,
                IsNullableRaw = true,
                Description = "Validation errors. Present only on 400 responses from endpoint validators.",
            };
            errorsProperty.Item = new JsonSchema { Reference = errorItemSchema };
            schema.Properties["errors"] = errorsProperty;
        }
    }

    private static void ReplaceSchemaReferences(
        OpenApiDocument document,
        JsonSchema oldSchema,
        JsonSchema newSchema)
    {
        foreach (var pathItem in document.Paths.Values)
        {
            foreach (var operation in pathItem.Values)
            {
                ReplaceInOperation(operation, oldSchema, newSchema);
            }
        }
    }

    private static void ReplaceInOperation(OpenApiOperation operation, JsonSchema oldSchema, JsonSchema newSchema)
    {
        foreach (var response in operation.Responses.Values)
        {
            foreach (var mediaType in response.Content.Values)
            {
                if (mediaType.Schema?.Reference == oldSchema)
                {
                    mediaType.Schema = new JsonSchema { Reference = newSchema };
                }
            }
        }
    }
}
