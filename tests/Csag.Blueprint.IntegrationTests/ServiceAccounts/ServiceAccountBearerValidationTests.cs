namespace Csag.Blueprint.IntegrationTests.ServiceAccounts;

using System.Net;
using System.Text;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.ServiceAccounts;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// End-to-end tests for the service-account bearer scheme. Each refused token differs from an accepted one in one
/// respect only — it names a live session of the seeded account, so the refusal is attributable to that respect: the
/// algorithm, the issuer, the audience, the expiry, the session, its subject.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class ServiceAccountBearerValidationTests(AppFixture app) : ServiceAccountTestBase(app)
{
    [Fact]
    public async Task ATokenMintedLikeTheIssuedOne_IsAcceptedAsync()
    {
        // The baseline every refusal below departs from.
        var sessionKey = await this.IssueSessionKeyAsync();

        await this.ShouldAuthenticateAsync(this.Mint(sessionKey), HttpStatusCode.OK);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherAlgorithm_IsRefusedAsync()
    {
        var sessionKey = await this.IssueSessionKeyAsync();

        await this.ShouldAuthenticateAsync(this.Mint(sessionKey, algorithm: SecurityAlgorithms.HmacSha384), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnUnsignedToken_IsRefusedAsync()
    {
        var sessionKey = await this.IssueSessionKeyAsync();

        await this.ShouldAuthenticateAsync(this.Mint(sessionKey, descriptor => descriptor.SigningCredentials = null), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenForAnotherIssuerOrAudience_IsRefusedAsync()
    {
        var sessionKey = await this.IssueSessionKeyAsync();

        await this.ShouldAuthenticateAsync(this.Mint(sessionKey, descriptor => descriptor.Issuer = "someone-else"), HttpStatusCode.Unauthorized);
        await this.ShouldAuthenticateAsync(this.Mint(sessionKey, descriptor => descriptor.Audience = "someone-else"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnExpiredToken_IsRefusedAsync()
    {
        var sessionKey = await this.IssueSessionKeyAsync();

        var expired = this.Mint(sessionKey, descriptor =>
        {
            descriptor.IssuedAt = DateTime.UtcNow.AddHours(-2);
            descriptor.NotBefore = DateTime.UtcNow.AddHours(-2);
            descriptor.Expires = DateTime.UtcNow.AddSeconds(-1);
        });

        await this.ShouldAuthenticateAsync(expired, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenWithoutASession_IsRefusedAsync()
    {
        await this.IssueSessionKeyAsync();

        await this.ShouldAuthenticateAsync(this.Mint(sessionKey: null), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenWhoseSessionWasRevoked_IsRefusedAsync()
    {
        var sessionKey = await this.IssueSessionKeyAsync();
        var token = this.Mint(sessionKey);
        await this.ShouldAuthenticateAsync(token, HttpStatusCode.OK);

        using (var scope = this.App.Services.CreateScope())
        {
            (await ServiceAccountSessionManagerAccessor.Resolve(scope.ServiceProvider)
                .RevokeSessionAsync(sessionKey, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        await this.ShouldAuthenticateAsync(token, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenWhoseSubjectDoesNotOwnTheSession_IsRefusedAsync()
    {
        var sessionKey = await this.IssueSessionKeyAsync();

        await this.ShouldAuthenticateAsync(this.Mint(sessionKey, subject: "sa-someone-else"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ARefusedToken_GetsABareBearerChallengeAsync()
    {
        using var client = this.BearerClient("not-a-token");

        using var response = await client.GetAsync(WhoAmIUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldHaveSingleItem().ToString().ShouldBe("Bearer");
    }

    private async Task<string> IssueSessionKeyAsync()
    {
        var token = await this.IssueAsync(SeedData.ServiceAccountClientId, SeedData.ServiceAccountSecret);
        return new JsonWebToken(token.AccessToken).GetClaim(ServiceAccountClaimTypes.SessionId).Value;
    }

    private async Task ShouldAuthenticateAsync(string token, HttpStatusCode expected)
    {
        using var client = this.BearerClient(token);
        await WhoAmIAsync(client, expected);
    }

    private string Mint(
        string? sessionKey,
        Action<SecurityTokenDescriptor>? change = null,
        string algorithm = SecurityAlgorithms.HmacSha256,
        string subject = SeedData.ServiceAccountClientId)
    {
        var options = this.App.Services.GetRequiredService<IOptions<BlueprintServiceAccountOptions>>().Value;
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            [ServiceAccountClaimTypes.Subject] = subject,
            [ServiceAccountClaimTypes.Name] = SeedData.ServiceAccountName,
            [ServiceAccountClaimTypes.Type] = ServiceAccountClaimTypes.ServiceAccountType,
        };

        if (sessionKey is not null)
        {
            claims[ServiceAccountClaimTypes.SessionId] = sessionKey;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!)), algorithm),
            Claims = claims,
        };
        change?.Invoke(descriptor);

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }
}
