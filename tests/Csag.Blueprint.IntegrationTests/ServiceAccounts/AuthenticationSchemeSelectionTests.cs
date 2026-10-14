namespace Csag.Blueprint.IntegrationTests.ServiceAccounts;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Testing.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// End-to-end tests for how the host picks the scheme that authenticates a request: endpoints list one selector
/// scheme, a session cookie alone authenticates as the user, a bearer token alone as the service account, and a
/// request carrying both is refused before either authenticates.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class AuthenticationSchemeSelectionTests(AppFixture app) : ServiceAccountTestBase(app)
{
    [Fact]
    public async Task AnEndpointWithoutItsOwnScheme_ListsTheOneSelectorSchemeAsync()
    {
        var policy = await this.EffectivePolicyAsync("/api/auth/whoami");

        policy.AuthenticationSchemes.ShouldBe([BlueprintAuthenticationSchemes.SessionOrBearer]);
    }

    [Fact]
    public async Task AnEndpointWithItsOwnScheme_ListsOnlyThatSchemeAsync()
    {
        var policy = await this.EffectivePolicyAsync("/api/auth/whoami/bearer");

        policy.AuthenticationSchemes.ShouldBe([BlueprintAuthenticationSchemes.Bearer]);
    }

    [Fact]
    public async Task ASessionCookieAlone_AuthenticatesAsTheUserAsync()
    {
        var principal = (await WhoAmIAsync(this.App.ManagerAClient, HttpStatusCode.OK)).ShouldNotBeNull();

        principal.AuthenticationTypes.ShouldBe([IdentityConstants.ApplicationScheme]);
        principal.Claims.ShouldNotContain(c => c.Type == ServiceAccountClaimTypes.Type);
    }

    [Fact]
    public async Task ABearerTokenAlone_AuthenticatesAsTheServiceAccountAsync()
    {
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);
        using var client = this.BearerClient(token.AccessToken);

        var principal = (await WhoAmIAsync(client, HttpStatusCode.OK)).ShouldNotBeNull();

        principal.AuthenticationTypes.ShouldBe([BlueprintAuthenticationSchemes.ServiceAccountToken]);
        principal.Claims.ShouldContain(c => c.Type == ServiceAccountClaimTypes.Subject && c.Value == SeedData.ServiceAccountClientId);
    }

    [Fact]
    public async Task ASessionCookieAndABearerToken_AreRefusedWith400ProblemDetailsAsync()
    {
        // Arrange — a signed-in client that also sends a valid service-account token.
        var ct = TestContext.Current.CancellationToken;
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);
        using var request = new HttpRequestMessage(HttpMethod.Get, WhoAmIUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // Act
        using var response = await this.App.ManagerAClient.SendAsync(request, ct);

        // Assert
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        json.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ASessionCookieAndABearerToken_AreRefusedOnAnAnonymousEndpointTooAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUri)
        {
            Content = JsonContent(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "any-token");

        using var response = await this.App.ManagerAClient.SendAsync(request, ct);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.BadRequest, cancellationToken: ct);
    }

    [Fact]
    public async Task ASessionCookieOnABearerOnlyEndpoint_IsUnauthenticatedAsync()
    {
        // The endpoint's own scheme replaces the host default, so the cookie is not consulted at all.
        await WhoAmIAsync(this.App.ManagerAClient, HttpStatusCode.Unauthorized, WhoAmIBearerUri);
    }

    [Fact]
    public async Task ABearerTokenOnABearerOnlyEndpoint_AuthenticatesAsync()
    {
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);
        using var client = this.BearerClient(token.AccessToken);

        var principal = (await WhoAmIAsync(client, HttpStatusCode.OK, WhoAmIBearerUri)).ShouldNotBeNull();

        principal.AuthenticationTypes.ShouldBe([BlueprintAuthenticationSchemes.ServiceAccountToken]);
    }

    private static StringContent JsonContent(string clientId, string clientSecret)
        => new(
            JsonSerializer.Serialize(new { clientId, clientSecret }),
            System.Text.Encoding.UTF8,
            "application/json");

    /// <summary>
    /// Combines an endpoint's authorization metadata the way the authorization middleware does.
    /// </summary>
    private async Task<AuthorizationPolicy> EffectivePolicyAsync(string route)
    {
        var endpoint = this.App.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => string.Equals("/" + e.RoutePattern.RawText?.TrimStart('/'), route, StringComparison.OrdinalIgnoreCase)
                && e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);

        var policy = await AuthorizationPolicy.CombineAsync(
            this.App.Services.GetRequiredService<IAuthorizationPolicyProvider>(),
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>());

        return policy.ShouldNotBeNull();
    }
}
