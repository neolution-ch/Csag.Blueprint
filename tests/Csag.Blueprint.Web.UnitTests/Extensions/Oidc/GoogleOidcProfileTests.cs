namespace Csag.Blueprint.Web.UnitTests.Extensions.Oidc;

using Csag.Blueprint.Web.Extensions.Oidc;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Unit tests for <see cref="GoogleOidcProfile"/>. Google behaves like the generic profile except the
/// authority defaults to Google's issuer, so only client credentials need configuring; these tests pin
/// the default, the explicit override, and that the generic issuer-validation knobs still apply.
/// </summary>
public sealed class GoogleOidcProfileTests
{
    private readonly GoogleOidcProfile profile = new();

    [Fact]
    public void Configure_NoAuthorityConfigured_DefaultsToGoogleIssuer()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.Authority.ShouldBe("https://accounts.google.com");
    }

    [Fact]
    public void Configure_ExplicitAuthority_OverridesGoogleDefault()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.Authority = "https://google-proxy.example.com";

        this.profile.Configure(options, settings);

        options.Authority.ShouldBe("https://google-proxy.example.com");
    }

    [Fact]
    public void Configure_DefaultIssuerValidation_KeepsDiscoveryDerivedIssuer()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidIssuers.ShouldBeNull();
        options.TokenValidationParameters.IssuerValidator.ShouldBeNull();
    }

    [Fact]
    public void Configure_ExplicitValidIssuers_AreApplied()
    {
        var options = new OpenIdConnectOptions();
        var settings = CreateSettings();
        settings.ValidIssuers = new List<string> { "https://accounts.google.com", "accounts.google.com" };

        this.profile.Configure(options, settings);

        options.TokenValidationParameters.ValidIssuers.ShouldBe(settings.ValidIssuers);
    }

    [Fact]
    public void Configure_MapsEmailVerifiedFromUserInfo()
    {
        var options = new OpenIdConnectOptions();

        this.profile.Configure(options, CreateSettings());

        options.ClaimActions.OfType<JsonKeyClaimAction>()
            .ShouldContain(action => action.ClaimType == "email_verified" && action.JsonKey == "email_verified");
    }

    private static OidcProviderSettings CreateSettings() => new()
    {
        Enabled = true,
        Profile = OidcProviderProfile.Google,
        ClientId = "client-id",
        ClientSecret = "client-secret",
    };
}
