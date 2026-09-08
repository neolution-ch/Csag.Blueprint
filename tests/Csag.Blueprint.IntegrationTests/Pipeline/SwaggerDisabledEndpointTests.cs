namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;

/// <summary>
/// Runtime proof of the secure-by-default Swagger behavior: when
/// Blueprint:Security:Swagger:Enabled is false (the Production default), BOTH the Swagger UI HTML
/// and the runtime Swagger JSON document return 404 — not just the UI. This exercises the gated
/// pipeline branch end-to-end against a host booted with the flag off, complementing
/// <see cref="SwaggerEndpointTests"/> (the enabled path). A refactor that reintroduced an
/// unconditional UseSwaggerGen() would pass the enabled tests but fail here. These tests never
/// touch the database, so the class runs outside the shared fixture collection with its own host.
/// </summary>
public sealed class SwaggerDisabledEndpointTests(SwaggerDisabledAppFixture app) : IClassFixture<SwaggerDisabledAppFixture>
{
    private readonly SwaggerDisabledAppFixture app = app;

    [Fact]
    public async Task SwaggerJson_Returns404_WhenDisabledAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = this.app.CreateClient();

        // The runtime OpenAPI document endpoint.
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative), ct);

        // The spec itself is not exposed (not merely the UI), so the API surface does not leak.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the runtime Swagger JSON document must not be served when disabled");
    }

    [Fact]
    public async Task SwaggerUi_Returns404_WhenDisabledAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = this.app.CreateClient();

        using var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the Swagger UI must not be served when disabled");
    }
}
