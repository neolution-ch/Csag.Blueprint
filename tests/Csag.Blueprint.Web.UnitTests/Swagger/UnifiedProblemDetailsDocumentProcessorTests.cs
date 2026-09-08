namespace Csag.Blueprint.Web.UnitTests.Swagger;

using Csag.Blueprint.Web.Swagger;
using NJsonSchema;
using NJsonSchema.Generation;
using NSwag;
using NSwag.Generation;
using NSwag.Generation.Processors.Contexts;

/// <summary>
/// Unit tests for <see cref="UnifiedProblemDetailsDocumentProcessor"/>.
/// </summary>
/// <remarks>
/// Two CLR types named <c>ProblemDetails</c> reach the document (ASP.NET Core's and FastEndpoints'),
/// so NSwag names one of them <c>ProblemDetails</c> and suffixes the other <c>ProblemDetails2</c>.
/// Which one wins depends on endpoint discovery order, i.e. on compile order. The processor folds the
/// suffixed aliases into the canonical schema; if it removes an alias while a reference to it is still
/// live anywhere in the document, serialization fails with "Could not find the JSON path of a
/// referenced schema" and the build breaks - which is exactly what renaming an endpoint folder used to
/// trigger. These tests pin that the fold covers the whole document, not just response bodies.
/// </remarks>
public sealed class UnifiedProblemDetailsDocumentProcessorTests
{
    [Fact]
    public void Process_AliasReferencedFromResponseBody_IsRedirectedAndDocumentSerializes()
    {
        var document = CreateDocumentWithAlias(out var alias);
        document.Paths["/things"] = new OpenApiPathItem
        {
            [OpenApiOperationMethod.Get] = new OpenApiOperation
            {
                Responses =
                {
                    ["404"] = new OpenApiResponse
                    {
                        Content = { ["application/problem+json"] = new OpenApiMediaType { Schema = new JsonSchema { Reference = alias } } },
                    },
                },
            },
        };

        Process(document);

        document.Definitions.ShouldNotContainKey("ProblemDetails2");
        Should.NotThrow(() => document.ToJson());
    }

    [Fact]
    public void Process_AliasReferencedFromRequestBody_IsRedirectedAndDocumentSerializes()
    {
        // The original implementation only rewrote response bodies, so a request-body reference
        // survived the removal of the alias and made the document unserializable.
        var document = CreateDocumentWithAlias(out var alias);
        document.Paths["/things"] = new OpenApiPathItem
        {
            [OpenApiOperationMethod.Post] = new OpenApiOperation
            {
                RequestBody = new OpenApiRequestBody
                {
                    Content = { ["application/json"] = new OpenApiMediaType { Schema = new JsonSchema { Reference = alias } } },
                },
            },
        };

        Process(document);

        document.Definitions.ShouldNotContainKey("ProblemDetails2");
        Should.NotThrow(() => document.ToJson());
    }

    [Fact]
    public void Process_AliasReferencedFromAnotherDefinitionProperty_IsRedirectedAndDocumentSerializes()
    {
        // A nested property reference is likewise out of reach of a response-only rewrite.
        var document = CreateDocumentWithAlias(out var alias);
        var envelope = new JsonSchema { Type = JsonObjectType.Object };
        envelope.Properties["error"] = new JsonSchemaProperty { Reference = alias };
        document.Definitions["Envelope"] = envelope;

        Process(document);

        document.Definitions.ShouldNotContainKey("ProblemDetails2");
        Should.NotThrow(() => document.ToJson());
    }

    [Fact]
    public void Process_WithoutAnyAlias_LeavesTheCanonicalSchemaUnified()
    {
        var document = new OpenApiDocument();
        document.Definitions["ProblemDetails"] = new JsonSchema { Type = JsonObjectType.Object };

        Process(document);

        var canonical = document.Definitions["ProblemDetails"];
        canonical.Properties.Keys.ShouldBe(
            ["type", "title", "status", "traceId", "detail", "instance"],
            ignoreOrder: true);
        Should.NotThrow(() => document.ToJson());
    }

    private static OpenApiDocument CreateDocumentWithAlias(out JsonSchema alias)
    {
        var document = new OpenApiDocument();
        document.Definitions["ProblemDetails"] = new JsonSchema { Type = JsonObjectType.Object };

        alias = new JsonSchema { Type = JsonObjectType.Object };
        alias.Properties["detail"] = new JsonSchemaProperty { Type = JsonObjectType.String };
        document.Definitions["ProblemDetails2"] = alias;

        return document;
    }

    private static void Process(OpenApiDocument document)
    {
        var settings = new OpenApiDocumentGeneratorSettings();
        var resolver = new JsonSchemaResolver(document, settings.SchemaSettings);
        var generator = new JsonSchemaGenerator(settings.SchemaSettings);
        var context = new DocumentProcessorContext(document, [], [], resolver, generator, settings);

        new UnifiedProblemDetailsDocumentProcessor().Process(context);
    }
}
