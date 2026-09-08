namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using System.Security.Claims;
using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Unit tests for <see cref="EntraClaimPolicy"/> — the security-critical Entra issuer-validation and
/// email-trust (nOAuth mitigation) logic.
/// </summary>
public sealed class EntraClaimPolicyTests
{
    private const string Tid = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void ValidateMultiTenantIssuer_V2IssuerMatchingTid_ReturnsIssuer()
    {
        var issuer = $"https://login.microsoftonline.com/{Tid}/v2.0";

        var result = EntraClaimPolicy.ValidateMultiTenantIssuer(issuer, CreateToken(Tid), new TokenValidationParameters());

        result.ShouldBe(issuer);
    }

    [Fact]
    public void ValidateMultiTenantIssuer_LegacyStsIssuerMatchingTid_ReturnsIssuer()
    {
        var issuer = $"https://sts.windows.net/{Tid}/";

        var result = EntraClaimPolicy.ValidateMultiTenantIssuer(issuer, CreateToken(Tid), new TokenValidationParameters());

        result.ShouldBe(issuer);
    }

    [Fact]
    public void ValidateMultiTenantIssuer_IssuerForDifferentTid_Throws()
    {
        var issuer = "https://login.microsoftonline.com/22222222-2222-2222-2222-222222222222/v2.0";

        Should.Throw<SecurityTokenInvalidIssuerException>(() =>
            EntraClaimPolicy.ValidateMultiTenantIssuer(issuer, CreateToken(Tid), new TokenValidationParameters()));
    }

    [Fact]
    public void ValidateMultiTenantIssuer_MissingTid_Throws()
    {
        var issuer = $"https://login.microsoftonline.com/{Tid}/v2.0";

        Should.Throw<SecurityTokenInvalidIssuerException>(() =>
            EntraClaimPolicy.ValidateMultiTenantIssuer(issuer, CreateToken(null), new TokenValidationParameters()));
    }

    [Fact]
    public void ValidateMultiTenantIssuer_NonJsonWebToken_Throws()
    {
        var issuer = $"https://login.microsoftonline.com/{Tid}/v2.0";

        Should.Throw<SecurityTokenInvalidIssuerException>(() =>
            EntraClaimPolicy.ValidateMultiTenantIssuer(issuer, new FakeSecurityToken(), new TokenValidationParameters()));
    }

    [Theory]
    [InlineData(MicrosoftEntraSignInAudience.SingleTenant, null, true)]
    [InlineData(MicrosoftEntraSignInAudience.MultiTenant, "true", true)]
    [InlineData(MicrosoftEntraSignInAudience.MultiTenant, "1", true)]
    [InlineData(MicrosoftEntraSignInAudience.MultiTenant, null, false)]
    [InlineData(MicrosoftEntraSignInAudience.MultiTenant, "false", false)]
    [InlineData(MicrosoftEntraSignInAudience.MultiTenantAndPersonal, null, false)]
    public void IsEmailVerified_ReturnsExpected(MicrosoftEntraSignInAudience audience, string? xmsEdov, bool expected)
    {
        var claims = new List<Claim>();
        if (xmsEdov != null)
        {
            claims.Add(new Claim("xms_edov", xmsEdov));
        }

        var identity = new ClaimsIdentity(claims, "test");

        EntraClaimPolicy.IsEmailVerified(identity, audience).ShouldBe(expected);
    }

    [Fact]
    public void NormalizeClaimsForCallback_MapsShortClaimsToStandardTypes()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-123"),
                new Claim("email", "user@example.com"),
                new Claim("given_name", "Ada"),
                new Claim("family_name", "Lovelace"),
            ],
            "test");

        EntraClaimPolicy.NormalizeClaimsForCallback(identity, MicrosoftEntraSignInAudience.SingleTenant);

        identity.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe("user-123");
        identity.FindFirst(ClaimTypes.Email)!.Value.ShouldBe("user@example.com");
        identity.FindFirst(ClaimTypes.GivenName)!.Value.ShouldBe("Ada");
        identity.FindFirst(ClaimTypes.Surname)!.Value.ShouldBe("Lovelace");
        identity.FindFirst("email_verified")!.Value.ShouldBe("true");
    }

    [Fact]
    public void NormalizeClaimsForCallback_MultiTenantWithoutEdov_StampsEmailNotVerified()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "u"), new Claim("email", "a@b.com")], "test");

        EntraClaimPolicy.NormalizeClaimsForCallback(identity, MicrosoftEntraSignInAudience.MultiTenant);

        identity.FindFirst("email_verified")!.Value.ShouldBe("false");
    }

    [Fact]
    public void NormalizeClaimsForCallback_MultiTenantWithIncomingVerifiedTrue_OverwritesToFalse()
    {
        // Fail closed: an email_verified value that arrived in the token/userinfo must never be trusted.
        // The computed trust value replaces it, so a forged "true" cannot re-open the nOAuth class.
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "u"),
                new Claim("email", "a@b.com"),
                new Claim("email_verified", "true"),
            ],
            "test");

        EntraClaimPolicy.NormalizeClaimsForCallback(identity, MicrosoftEntraSignInAudience.MultiTenant);

        identity.FindAll("email_verified").Count().ShouldBe(1);
        identity.FindFirst("email_verified")!.Value.ShouldBe("false");
    }

    [Fact]
    public void NormalizeClaimsForCallback_VerifiedWithoutEmail_FallsBackToPreferredUsername()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "u"),
                new Claim("preferred_username", "worker@contoso.com"),
                new Claim("xms_edov", "true"),
            ],
            "test");

        EntraClaimPolicy.NormalizeClaimsForCallback(identity, MicrosoftEntraSignInAudience.MultiTenant);

        identity.FindFirst(ClaimTypes.Email)!.Value.ShouldBe("worker@contoso.com");
    }

    [Fact]
    public void NormalizeClaimsForCallback_UnverifiedWithoutEmail_DoesNotTrustPreferredUsername()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "u"),
                new Claim("preferred_username", "attacker@contoso.com"),
            ],
            "test");

        EntraClaimPolicy.NormalizeClaimsForCallback(identity, MicrosoftEntraSignInAudience.MultiTenant);

        identity.FindFirst(ClaimTypes.Email).ShouldBeNull();
    }

    private static JsonWebToken CreateToken(string? tid)
    {
        var payload = tid is null ? "{}" : $"{{\"tid\":\"{tid}\"}}";
        return new JsonWebToken("{\"alg\":\"none\",\"typ\":\"JWT\"}", payload);
    }

    private sealed class FakeSecurityToken : SecurityToken
    {
        public override string Id => "fake";

        public override string Issuer => "fake";

        public override SecurityKey SecurityKey => null!;

        public override SecurityKey SigningKey { get; set; } = null!;

        public override DateTime ValidFrom => DateTime.UtcNow;

        public override DateTime ValidTo => DateTime.UtcNow;
    }
}
