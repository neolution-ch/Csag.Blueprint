namespace Csag.Blueprint.TestHost.Swagger;

using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

/// <summary>
/// Registered through <c>BlueprintServicesOptions.ConfigureOpenApiDocument</c>. It proves, end to
/// end, that a host-supplied document processor runs, and that it runs after the Blueprint
/// defaults. Its operation summary records whether the canonical "ProblemDetails" schema already
/// stands alone, with no numbered duplicate. That proves <c>UnifiedProblemDetailsDocumentProcessor</c>,
/// a Blueprint default, already ran.
/// </summary>
public sealed class TestMarkerDocumentProcessor : IDocumentProcessor
{
    /// <summary>
    /// Gets the path this processor adds to the generated document.
    /// </summary>
    public static string MarkerPath => "/test-marker";

    /// <summary>
    /// Gets the operation summary recorded when the Blueprint defaults already ran.
    /// </summary>
    public static string BlueprintDefaultsAppliedMarker => "blueprint-defaults-applied";

    /// <inheritdoc/>
    public void Process(DocumentProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var schemaNames = context.Document.Definitions.Keys;

        // A removed alias is "ProblemDetails" with a numeric suffix, for example ProblemDetails2,
        // per UnifiedProblemDetailsDocumentProcessor.IsSuffixedAlias. "ProblemDetails_Error" is a
        // separate, real schema that processor itself references. It is not a duplicate to remove.
        var defaultsApplied = schemaNames.Contains("ProblemDetails")
            && !schemaNames.Any(name =>
                name.StartsWith("ProblemDetails", StringComparison.Ordinal)
                && name.Length > "ProblemDetails".Length
                && name["ProblemDetails".Length..].All(char.IsAsciiDigit));

        var operation = new OpenApiOperation
        {
            OperationId = "GetTestMarker",
            Summary = defaultsApplied ? BlueprintDefaultsAppliedMarker : "blueprint-defaults-not-yet-applied",
        };

        // OpenAPI requires at least one response per operation; a bare 200 keeps the document valid.
        operation.Responses["200"] = new OpenApiResponse { Description = "Test marker." };

        context.Document.Paths.TryAdd(MarkerPath, new OpenApiPathItem
        {
            [OpenApiOperationMethod.Get] = operation,
        });
    }
}
