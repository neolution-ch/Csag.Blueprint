namespace Csag.Blueprint.IntegrationTests.ServiceAccounts;

using System.Globalization;
using System.Net;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Authentication;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Session;
using Csag.Blueprint.TestHost.Time;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

/// <summary>
/// End-to-end tests for issuing service-account tokens through the host's token endpoint shell: the response contract,
/// the token's claims, the principal it authenticates as, and every refusal — unknown client, wrong secret, inactive
/// account with the right and the wrong secret, lockout and its expiry on the injected clock.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ServiceAccountTokenTests(AppFixture app) : ServiceAccountTestBase(app)
{
    [Fact]
    public async Task Issue_WithTheSeededCredentials_ReturnsABearerTokenForAnHourAsync()
    {
        // Act
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);

        // Assert — the wire contract: { accessToken, tokenType, expiresIn }.
        token.TokenType.ShouldBe("Bearer");
        token.ExpiresIn.ShouldBe(3600);
        token.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Issue_TheTokenCarriesTheNamedClaimsAndNoAuthorizationAsync()
    {
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);

        var jwt = new JsonWebToken(token.AccessToken);

        // The claim names round-trip through ServiceAccountClaimTypes; the token is a reference to its session and
        // carries no tenant, role or permission of its own.
        jwt.Alg.ShouldBe("HS256");
        jwt.GetClaim(ServiceAccountClaimTypes.Subject).Value.ShouldBe(SeedData.ServiceAccountClientId);
        jwt.GetClaim(ServiceAccountClaimTypes.Name).Value.ShouldBe(SeedData.ServiceAccountName);
        jwt.GetClaim(ServiceAccountClaimTypes.Type).Value.ShouldBe(ServiceAccountClaimTypes.ServiceAccountType);
        jwt.GetClaim(ServiceAccountClaimTypes.SessionId).Value.ShouldNotBeNullOrWhiteSpace();
        jwt.Claims.Select(c => c.Type).ShouldNotContain(IdentityClaimTypes.TenantId);
        jwt.Claims.Select(c => c.Type).ShouldNotContain(IdentityClaimTypes.Permission);
    }

    [Fact]
    public async Task Issue_TheTokenAuthenticatesAsTheAccountWithItsCurrentGrantsAsync()
    {
        // Arrange
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);
        using var client = this.BearerClient(token.AccessToken);

        // Act
        var principal = (await WhoAmIAsync(client, HttpStatusCode.OK)).ShouldNotBeNull();

        // Assert — one identity, from the service-account scheme, built from the account and enriched by the host.
        principal.AuthenticationTypes.ShouldBe([BlueprintAuthenticationSchemes.ServiceAccountToken]);
        string? Claim(string type) => principal.Claims.FirstOrDefault(c => c.Type == type)?.Value;
        Claim(ServiceAccountClaimTypes.Subject).ShouldBe(SeedData.ServiceAccountClientId);
        Claim(ServiceAccountClaimTypes.Name).ShouldBe(SeedData.ServiceAccountName);
        Claim(ServiceAccountClaimTypes.Type).ShouldBe(ServiceAccountClaimTypes.ServiceAccountType);
        Claim(IdentityClaimTypes.TenantId).ShouldBe(SeedData.TenantAId.ToString("D", CultureInfo.InvariantCulture));
        Claim(TestHostServiceAccountClaimsEnricher.ClaimType).ShouldBe(SeedData.ServiceAccountId.ToString("D", CultureInfo.InvariantCulture));
        principal.Claims.Where(c => c.Type == IdentityClaimTypes.Permission).Select(c => c.Value).ShouldContain(TestPermissions.VehiclesRead);

        // The role-derived permission authorizes a tenant endpoint.
        using var vehicles = await client.GetAsync(new Uri("/api/vehicles", UriKind.Relative), TestContext.Current.CancellationToken);
        await vehicles.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Issue_ForAnUnknownClient_Returns401Async()
    {
        using var response = await this.RequestTokenAsync("sa-no-such-client", SeedData.ServiceAccountSecret);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("sa-client", " ")]
    public async Task Issue_WithABlankField_Returns401Async(string clientId, string clientSecret)
    {
        using var response = await this.RequestTokenAsync(clientId, clientSecret);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Issue_WithAWrongSecret_Returns401AndCountsTheAttemptAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("wrong secret");

        using var response = await this.RequestTokenAsync(credentials.ClientId, "not-the-secret");

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
        (await this.ReadAccountAsync(id)).ShouldNotBeNull().AccessFailedCount.ShouldBe(1);
        (await this.CountSessionsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task Issue_WithTheRightSecret_ClearsTheFailureCountAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("clears count");
        (await this.RequestTokenAsync(credentials.ClientId, "not-the-secret")).Dispose();

        await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);

        (await this.ReadAccountAsync(id)).ShouldNotBeNull().AccessFailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Issue_ForAnInactiveAccountWithTheRightSecret_Returns403Async()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("inactive, right secret");
        await this.UpdateAccountAsync(id, account => account.IsActive = false);

        using var response = await this.RequestTokenAsync(credentials.ClientId, credentials.ClientSecret);

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Forbidden, cancellationToken: TestContext.Current.CancellationToken);
        (await this.CountSessionsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task Issue_ForAnInactiveAccountWithAWrongSecret_Returns401AndCountsTheAttemptAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("inactive, wrong secret");
        await this.UpdateAccountAsync(id, account => account.IsActive = false);

        using var response = await this.RequestTokenAsync(credentials.ClientId, "not-the-secret");

        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
        (await this.ReadAccountAsync(id)).ShouldNotBeNull().AccessFailedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Issue_AfterTheMaximumWrongSecrets_RefusesEvenTheRightSecretAsync()
    {
        // Arrange — the host locks an account after five wrong secrets.
        var (id, credentials) = await this.CreateServiceAccountAsync("locked out");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await this.RequestTokenAsync(credentials.ClientId, "not-the-secret")).Dispose();
        }

        // Act
        using var response = await this.RequestTokenAsync(credentials.ClientId, credentials.ClientSecret);

        // Assert — the same answer as a wrong secret, and the lockout stands.
        await response.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
        var account = (await this.ReadAccountAsync(id)).ShouldNotBeNull();
        account.LockoutEnd.ShouldNotBeNull();
        account.AccessFailedCount.ShouldBe(0);
        (await this.CountSessionsAsync(id)).ShouldBe(0);
    }

    [Fact]
    public async Task Issue_WhileLockedOut_DoesNotCountFurtherWrongSecretsAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("locked, more wrong secrets");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await this.RequestTokenAsync(credentials.ClientId, "not-the-secret")).Dispose();
        }

        var lockoutEnd = (await this.ReadAccountAsync(id)).ShouldNotBeNull().LockoutEnd;
        (await this.RequestTokenAsync(credentials.ClientId, "not-the-secret")).Dispose();

        var account = (await this.ReadAccountAsync(id)).ShouldNotBeNull();
        account.AccessFailedCount.ShouldBe(0);
        account.LockoutEnd.ShouldBe(lockoutEnd);
    }

    [Fact]
    public async Task Issue_OnceTheLockoutHasPassedOnTheInjectedClock_IssuesAgainAsync()
    {
        // Arrange — locked by the wall clock, then the host clock moves past the five-minute lockout.
        var (id, credentials) = await this.CreateServiceAccountAsync("lockout expiry");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await this.RequestTokenAsync(credentials.ClientId, "not-the-secret")).Dispose();
        }

        var lockoutEnd = (await this.ReadAccountAsync(id)).ShouldNotBeNull().LockoutEnd.ShouldNotBeNull();
        var clock = this.App.Services.GetRequiredService<PinnableTimeProvider>();

        // Act & Assert — a second before the end it still refuses; at the end it issues, and clears the lockout.
        using (clock.Pin(lockoutEnd.AddSeconds(-1)))
        {
            using var stillLocked = await this.RequestTokenAsync(credentials.ClientId, credentials.ClientSecret);
            await stillLocked.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, cancellationToken: TestContext.Current.CancellationToken);
        }

        using (clock.Pin(lockoutEnd))
        {
            await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);
        }

        var account = (await this.ReadAccountAsync(id)).ShouldNotBeNull();
        account.LockoutEnd.ShouldBeNull();
        account.AccessFailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Issue_RecordsTheSessionWithTheTokensExpiryAsync()
    {
        var (id, credentials) = await this.CreateServiceAccountAsync("session record");
        var before = DateTimeOffset.UtcNow;

        await this.IssueAsync(credentials.ClientId, credentials.ClientSecret);

        using var scope = this.App.CreateDbContextScope();
        var session = scope.Context.ServiceAccountSessions.Single(s => s.ServiceAccountId == id);
        session.ExpiresAt.ShouldBeInRange(before.AddHours(1), DateTimeOffset.UtcNow.AddHours(1));
        session.SessionKey.Length.ShouldBe(43);
    }
}
