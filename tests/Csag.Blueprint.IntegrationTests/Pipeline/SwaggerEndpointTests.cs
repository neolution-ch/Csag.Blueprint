namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;
using System.Text.Json;

/// <summary>
/// End-to-end tests for the runtime Swagger surface when it is enabled. The shared fixture runs
/// in the Testing environment, where Blueprint:Security:Swagger:Enabled is true, so the Swagger
/// routes are exposed. The disabled counterpart is covered by
/// <see cref="SwaggerDisabledEndpointTests"/> with a separately booted host.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SwaggerEndpointTests(AppFixture app) : IntegrationTestBase(app)
{
    private const string CanonicalProblemDetails = "ProblemDetails";

    [Fact]
    public async Task SwaggerJson_WhenEnabled_SerializesWithOneUnifiedProblemDetailsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = this.App.CreateClient();

        // This host is the awkward case for Problem Details unification, which is why the assertion
        // is worth making end to end. Its first generated operation carries a FastEndpoints
        // validator, so FastEndpoints.ProblemDetails claims the "ProblemDetails" schema name and
        // Mvc.ProblemDetails - added to every operation by ProblemDetailsOperationProcessor - is
        // pushed into "ProblemDetails2". For every operation after the first, the schema generator
        // hands the operation processor a reference-wrapper schema, and wrapping that again produces
        // a chained reference. Serialization used to throw ("Could not find the JSON path of a
        // referenced schema"), answering 500, because UnifiedProblemDetailsDocumentProcessor removed
        // "ProblemDetails2" while those chains still pointed at it. It now redirects references
        // across the whole document, so the chains are repointed before the removal.
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var declared = schemas.EnumerateObject().Select(property => property.Name).ToList();

        // Exactly one Problem Details schema survives: no suffixed alias is left behind.
        declared.ShouldContain(CanonicalProblemDetails);
        declared.ShouldNotContain(
            name => IsSuffixedAlias(name),
            "every ProblemDetails alias must be folded into the canonical schema");

        // The unified shape always carries these six; "errors" is added on top only when the
        // validation-error item schema is present, which depends on the host's endpoints.
        var properties = schemas.GetProperty(CanonicalProblemDetails).GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToList();

        foreach (var required in new[] { "type", "title", "status", "traceId", "detail", "instance" })
        {
            properties.ShouldContain(required);
        }

        // Every $ref resolves to a declared schema. A dangling one is what used to break
        // serialization, so naming it here turns a bare 500 into a legible failure.
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        CollectSchemaReferences(document.RootElement, referenced);

        referenced.Except(declared, StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Fact]
    public async Task SwaggerUi_IsServed_WhenEnabledAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = this.App.CreateClient();

        using var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Determines whether a schema name is an NSwag-suffixed ProblemDetails duplicate.</summary>
    private static bool IsSuffixedAlias(string name)
        => name.StartsWith(CanonicalProblemDetails, StringComparison.Ordinal)
           && name.Length > CanonicalProblemDetails.Length
           && name[CanonicalProblemDetails.Length..].All(char.IsAsciiDigit);

    /// <summary>Collects the target name of every <c>$ref</c> into components/schemas.</summary>
    private static void CollectSchemaReferences(JsonElement element, HashSet<string> into)
    {
        const string prefix = "#/components/schemas/";

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "$ref", StringComparison.Ordinal)
                        && property.Value.ValueKind == JsonValueKind.String
                        && property.Value.GetString() is { } reference
                        && reference.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        into.Add(reference[prefix.Length..]);
                    }

                    CollectSchemaReferences(property.Value, into);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectSchemaReferences(item, into);
                }

                break;
        }
    }
}
