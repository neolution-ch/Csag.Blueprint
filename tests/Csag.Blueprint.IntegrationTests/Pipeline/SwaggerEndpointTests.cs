namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;

/// <summary>
/// End-to-end tests for the runtime Swagger surface when it is enabled. The shared fixture runs
/// in the Testing environment, where Blueprint:Security:Swagger:Enabled is true, so the Swagger
/// routes are exposed. The disabled counterpart is covered by
/// <see cref="SwaggerDisabledEndpointTests"/> with a separately booted host.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SwaggerEndpointTests(AppFixture app) : IntegrationTestBase(app)
{
    [Fact]
    public async Task SwaggerJson_WhenEnabled_CurrentlyFailsSerializationWithDanglingProblemDetailsRefsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = this.App.CreateClient();

        // NSwag serves the OpenAPI document at /swagger/{documentName}/swagger.json. The intended
        // contract is a 200 with the document, but serializing it currently throws ("Could not
        // find the JSON path of a referenced schema"), so the route answers 500 in this host:
        // the first operation NSwag generates here carries a FastEndpoints validator, which makes
        // FastEndpoints.ProblemDetails claim the "ProblemDetails" schema name and pushes
        // Mvc.ProblemDetails (added per operation by ProblemDetailsOperationProcessor) into
        // "ProblemDetails2". UnifiedProblemDetailsDocumentProcessor removes "ProblemDetails2" and
        // rewrites only direct references to it — but for every operation after the first, the
        // schema generator hands the operation processor a reference-wrapper schema, and wrapping
        // that again leaves a chained reference the document processor does not see. Those chains
        // still end at the removed schema, so the document no longer serializes. Hosts whose first
        // generated operation has no FastEndpoints validator register the schemas in the opposite
        // order and are unaffected, which is why the same package code can serve Swagger fine
        // elsewhere. Pinned so a package-side fix surfaces loudly here.
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SwaggerUi_IsServed_WhenEnabledAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = this.App.CreateClient();

        using var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
