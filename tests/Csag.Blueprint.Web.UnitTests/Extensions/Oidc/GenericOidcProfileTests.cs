namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Unit tests for <see cref="GenericOidcProfile"/> — the profile for any standards-compliant provider.
/// The configuration shared with all profiles is covered by <see cref="OidcProviderProfileBaseTests"/>;
/// these tests pin what the generic profile adds: the pass-through authority and the
/// <c>email_verified</c> claim mapping consumed by the callback's email-trust gate.
/// </summary>
public sealed class GenericOidcProfileTests
{
    private readonly GenericOidcProfile profile = new();

    [Fact]
    public void Configure_ConfiguredAuthority_IsApplied()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.Authority = "https://tenant.auth0.example.com";

        this.profile.Configure(options, settings);

        options.Authority.ShouldBe("https://tenant.auth0.example.com");
    }

    [Fact]
    public void Configure_BlankAuthority_LeavesAuthorityUnset()
    {
        // The generic profile has no provider to default to; a missing authority is a configuration
        // error caught by the settings validator, not silently patched here.
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.Authority = " ";

        this.profile.Configure(options, settings);

        options.Authority.ShouldBeNull();
    }

    [Fact]
    public void Configure_LeavesCallbackPathAtHandlerDefault()
    {
        // Per-provider callback paths are wired by the registration extension, not the profile.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.CallbackPath.Value.ShouldBe("/signin-oidc");
    }

    [Fact]
    public void Configure_MapsEmailVerifiedFromUserInfo()
    {
        // email_verified is not part of the default inbound claim map; the explicit MapJsonKey action
        // surfaces it from userinfo for providers that only send it there.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.ClaimActions.OfType<JsonKeyClaimAction>()
            .ShouldContain(action => action.ClaimType == "email_verified" && action.JsonKey == "email_verified");
    }

    [Fact]
    public void Configure_KeepsDefaultInboundClaimMapping()
    {
        // Unlike Entra, generic providers rely on the default inbound map to produce the standard
        // ClaimTypes the callback reads.
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.MapInboundClaims.ShouldBeTrue();
    }

    private static OidcProviderSettings CreateSettings() => new()
    {
        Enabled = true,
        ClientId = "client-id",
        ClientSecret = "client-secret",
    };
}
