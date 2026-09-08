namespace Csag.Blueprint.Web.Swagger;

using System.Globalization;
using NJsonSchema;
using NJsonSchema.References;
using NJsonSchema.Visitors;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

/// <summary>
/// Collapses every auto-generated Problem Details schema into a single unified one, so generated
/// clients get one consistent <c>ProblemDetails</c> error type rather than several separate types
/// coming from different C# libraries.
/// </summary>
/// <remarks>
/// <para>
/// Two distinct CLR types reach the document under the same schema name:
/// <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c>, added to every operation by
/// <see cref="ProblemDetailsOperationProcessor"/>, and FastEndpoints' own <c>ProblemDetails</c>,
/// used for validation failures. NSwag gives one of them the bare name and suffixes the rest
/// (<c>ProblemDetails2</c>, <c>ProblemDetails3</c>, ...). Which one wins the bare name depends on
/// the order in which the schema generator first encounters them, which follows endpoint discovery
/// order and therefore compile order - so it changes when endpoint folders are renamed or reordered.
/// </para>
/// <para>
/// This processor must therefore not assume a winner. It redirects references to the suffixed
/// aliases <b>everywhere in the document</b>, not just in response bodies, and only then removes
/// them. A single missed reference makes the document unserializable, failing the build with
/// "Could not find the JSON path of a referenced schema".
/// </para>
/// <para>
/// Runs as a document processor (after all operation processors) so it can rewrite the full
/// document in one pass. The unified schema matches what both ASP.NET Core and FastEndpoints
/// actually produce at runtime.
/// </para>
/// </remarks>
public class UnifiedProblemDetailsDocumentProcessor : IDocumentProcessor
{
    private const string CanonicalName = "ProblemDetails";

    /// <inheritdoc/>
    public void Process(DocumentProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var document = context.Document;

        // The canonical schema must exist (added by ProblemDetailsOperationProcessor).
        if (!document.Definitions.TryGetValue(CanonicalName, out var canonical))
        {
            return;
        }

        // Rebuild the canonical schema in-place so all existing references to it remain valid.
        RebuildAsUnifiedSchema(canonical, document);

        // Every "ProblemDetails<N>" definition is the same concept generated from a different CLR
        // type. Fold them into the canonical schema.
        var aliasNames = document.Definitions.Keys.Where(IsSuffixedAlias).ToList();

        if (aliasNames.Count == 0)
        {
            return;
        }

        var aliases = new HashSet<JsonSchema>(
            aliasNames.Select(name => document.Definitions[name]),
            ReferenceEqualityComparer.Instance);

        new AliasReferenceRedirector(canonical, aliases).Visit(document);

        foreach (var name in aliasNames)
        {
            document.Definitions.Remove(name);
        }
    }

    /// <summary>
    /// Determines whether a definition name is an NSwag-suffixed duplicate of the canonical schema,
    /// for example <c>ProblemDetails2</c>. The bare name itself is never an alias.
    /// </summary>
    /// <param name="name">The definition name.</param>
    /// <returns><c>true</c> when the name is a suffixed duplicate; otherwise <c>false</c>.</returns>
    private static bool IsSuffixedAlias(string name)
    {
        if (!name.StartsWith(CanonicalName, StringComparison.Ordinal) || name.Length == CanonicalName.Length)
        {
            return false;
        }

        return int.TryParse(
            name[CanonicalName.Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out _);
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

    /// <summary>
    /// Rewrites every reference to one of the alias schemas so it points at the canonical schema.
    /// </summary>
    /// <remarks>
    /// Walks the whole document graph rather than only response bodies: aliases are also reachable
    /// from request bodies, parameters, nested properties, array items and composition keywords, and
    /// any reference left pointing at a removed definition breaks serialization.
    /// </remarks>
    private sealed class AliasReferenceRedirector : JsonReferenceVisitorBase
    {
        private readonly JsonSchema canonical;
        private readonly HashSet<JsonSchema> aliases;

        public AliasReferenceRedirector(JsonSchema canonical, HashSet<JsonSchema> aliases)
        {
            this.canonical = canonical;
            this.aliases = aliases;
        }

        protected override IJsonReference VisitJsonReference(IJsonReference reference, string path, string? typeNameHint)
        {
            if (reference.Reference is JsonSchema target && this.aliases.Contains(target))
            {
                reference.Reference = this.canonical;
            }

            return reference;
        }
    }
}
