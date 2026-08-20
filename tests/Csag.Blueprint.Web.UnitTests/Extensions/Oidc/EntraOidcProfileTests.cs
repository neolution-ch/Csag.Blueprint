namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using System.Security.Claims;
using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Unit tests for <see cref="EntraOidcProfile"/>: the audience-derived authority, the short-claim
/// handling (<c>MapInboundClaims</c> off, <c>name</c>/<c>roles</c> claim types), the tenant-aware
/// issuer-validation selection (single-tenant keeps the discovery issuer; multi-tenant installs
/// <see cref="EntraClaimPolicy.ValidateMultiTenantIssuer"/>), and the <c>OnTokenValidated</c> hook
/// that normalizes claims for the shared callback.
/// </summary>
public sealed class EntraOidcProfileTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    private readonly EntraOidcProfile profile = new();

    [Fact]
    public void Configure_SingleTenant_DerivesTenantAuthority()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.SingleTenant));

        options.Authority.ShouldBe($"https://login.microsoftonline.com/{TenantId}/v2.0");
    }

    [Fact]
    public void Configure_MultiTenant_UsesOrganizationsAuthority()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenant));

        options.Authority.ShouldBe("https://login.microsoftonline.com/organizations/v2.0");
    }

    [Fact]
    public void Configure_MultiTenantAndPersonal_UsesCommonAuthority()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenantAndPersonal));

        options.Authority.ShouldBe("https://login.microsoftonline.com/common/v2.0");
    }

    [Fact]
    public void Configure_ExplicitAuthority_WinsOverAudienceDerivation()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings(MicrosoftEntraSignInAudience.MultiTenant);
        settings.Authority = "https://login.microsoftonline.us/organizations/v2.0";

        this.profile.Configure(options, settings);

        options.Authority.ShouldBe("https://login.microsoftonline.us/organizations/v2.0");
    }

    [Fact]
    public void Configure_UnknownSignInAudienceWithoutAuthority_Throws()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings((MicrosoftEntraSignInAudience)999);

        Should.Throw<InvalidOperationException>(() => this.profile.Configure(options, settings));
    }

    [Fact]
    public void Configure_NormalizesShortClaimHandling()
    {
        // Entra id_tokens use short claim names; the profile turns the inbound map off and points the
        // handler at Entra's "name"/"roles" claims instead.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.SingleTenant));

        options.MapInboundClaims.ShouldBeFalse();
        options.TokenValidationParameters.NameClaimType.ShouldBe("name");
        options.TokenValidationParameters.RoleClaimType.ShouldBe("roles");
    }

    [Fact]
    public void Configure_SingleTenant_KeepsDiscoveryIssuerValidation()
    {
        // A single-tenant app has exactly one issuer, so the discovery-derived issuer suffices and no
        // custom validator is installed.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.SingleTenant));

        options.TokenValidationParameters.IssuerValidator.ShouldBeNull();
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
    }

    [Fact]
    public void Configure_MultiTenant_InstallsTenantAwareIssuerValidator()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenant));

        options.TokenValidationParameters.IssuerValidator.ShouldBe((IssuerValidator)EntraClaimPolicy.ValidateMultiTenantIssuer);
    }

    [Fact]
    public void Configure_MultiTenantAndPersonal_InstallsValidatorAndForcesValidationOn()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings(MicrosoftEntraSignInAudience.MultiTenantAndPersonal));

        options.TokenValidationParameters.IssuerValidator.ShouldBe((IssuerValidator)EntraClaimPolicy.ValidateMultiTenantIssuer);
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
    }

    [Fact]
    public async Task Configure_OnTokenValidated_NormalizesClaimsForCallbackAsync()
    {
        // The wired event delegates to EntraClaimPolicy.NormalizeClaimsForCallback: the raw "sub" claim
        // is mapped to NameIdentifier and a computed email_verified claim is stamped ("true" here
        // because the audience is single-tenant).
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings(MicrosoftEntraSignInAudience.SingleTenant);
        this.profile.Configure(options, settings);

        var identity = new ClaimsIdentity(
            new[] { new Claim("sub", "user-1"), new Claim("email", "user@example.com") },
            authenticationType: "Test");
        var tokenValidatedContext = new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("TestScheme", displayName: null, typeof(OpenIdConnectHandler)),
            options,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties());

        await options.Events.OnTokenValidated(tokenValidatedContext);

        identity.FindFirst(ClaimTypes.NameIdentifier)!.Value.ShouldBe("user-1");
        identity.FindFirst(ClaimTypes.Email)!.Value.ShouldBe("user@example.com");
        identity.FindFirst("email_verified")!.Value.ShouldBe("true");
    }

    private static OidcProviderSettings CreateSettings(MicrosoftEntraSignInAudience audience) => new()
    {
        Enabled = true,
        Profile = OidcProviderProfile.Entra,
        ClientId = "client-id",
        ClientSecret = "client-secret",
        TenantId = TenantId,
        SignInAudience = audience,
    };
}
