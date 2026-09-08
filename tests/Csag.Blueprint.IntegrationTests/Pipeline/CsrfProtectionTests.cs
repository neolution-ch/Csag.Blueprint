namespace Csag.Blueprint.IntegrationTests.Pipeline;

using System.Net;
using System.Text.Json;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.Testing.Extensions;
using FastEndpoints;

/// <summary>
/// End-to-end tests for CSRF protection over the full cookie flow: the login response sets the
/// antiforgery cookie pair, and the middleware then rejects state-changing requests whose
/// X-CSRF-TOKEN header is missing or does not match the cookie, while letting safe methods and
/// header-carrying requests through. Unauthenticated callers are rejected by authentication (401)
/// before CSRF applies, because only cookie sessions are CSRF-vulnerable.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class CsrfProtectionTests(AppFixture app) : IntegrationTestBase(app)
{
    private static readonly Uri VehiclesUri = new("/api/vehicles", UriKind.Relative);

    [Fact]
    public async Task StateChangingRequest_WithoutCsrfHeader_Returns403Async()
    {
        var ct = TestContext.Current.CancellationToken;

        // Sign in so the cookie container holds a session and the antiforgery cookies,
        // but deliberately never set the X-CSRF-TOKEN header.
        using var client = await this.CreateSessionWithoutCsrfHeaderAsync(SeedData.ManagerAEmail);

        using var response = await client.DeleteAsync(new Uri($"/api/vehicles/{Guid.NewGuid()}", UriKind.Relative), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe("CSRF validation failed");
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(403);

        // The rejection is a problem-details payload carrying the request's correlation id.
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
        json.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task StateChangingRequest_WithValidCsrfHeader_PassesThroughAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // The shared manager client carries a valid CSRF header from fixture setup. Deleting a
        // non-existent vehicle proves the CSRF gate passed: 404 from the endpoint, not 403.
        using var response = await this.App.ManagerAClient.DeleteAsync(
            new Uri($"/api/vehicles/{Guid.NewGuid()}", UriKind.Relative), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SafeMethodRequest_WithoutCsrfHeader_SucceedsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = await this.CreateSessionWithoutCsrfHeaderAsync(SeedData.ViewerAEmail);

        // GET is a safe method and must not require a CSRF token.
        using var response = await client.GetAsync(VehiclesUri, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StateChangingRequest_WithWrongCsrfToken_Returns403Async()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = await this.CreateSessionWithoutCsrfHeaderAsync(SeedData.ManagerAEmail);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "definitely-not-a-valid-token");

        using var response = await client.DeleteAsync(new Uri($"/api/vehicles/{Guid.NewGuid()}", UriKind.Relative), ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnauthenticatedRequest_DeleteWithoutCsrf_Returns401NotCsrfErrorAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = this.App.CreateClient();

        using var response = await client.DeleteAsync(new Uri($"/api/vehicles/{Guid.NewGuid()}", UriKind.Relative), ct);

        // Authentication rejects the request before CSRF validation is ever reached: CSRF
        // protection only applies to cookie-authenticated callers.
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Signs the given seeded user in on a fresh client. The login response populates the cookie
    /// container (session + antiforgery cookies), but unlike the fixture's shared clients the
    /// X-CSRF-TOKEN default header is intentionally left unset.
    /// </summary>
    /// <param name="email">The seeded user's email.</param>
    /// <returns>An authenticated client without a CSRF request header.</returns>
    private async Task<HttpClient> CreateSessionWithoutCsrfHeaderAsync(string email)
    {
        var client = this.App.CreateClient();

        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = email,
                Password = SeedData.DefaultPassword,
            });

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);
        return client;
    }
}
