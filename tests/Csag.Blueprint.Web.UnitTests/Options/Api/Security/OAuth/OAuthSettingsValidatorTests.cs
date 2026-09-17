namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.OAuth;

using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="OAuthSettingsValidator"/> (FrontendBaseUrl format and
/// per-provider child validation).
/// </summary>
public sealed class OAuthSettingsValidatorTests
{
    private readonly OAuthSettingsValidator validator = new();

    [Fact]
    public void Validate_NullFrontendBaseUrl_Passes()
    {
        var settings = new OAuthSettings { FrontendBaseUrl = null };

        this.validator.TestValidate(settings).ShouldNotHaveValidationErrorFor(x => x.FrontendBaseUrl);
    }

    [Fact]
    public void Validate_AbsoluteHttpsFrontendBaseUrl_Passes()
    {
        var settings = new OAuthSettings { FrontendBaseUrl = "https://app.example.com" };

        this.validator.TestValidate(settings).ShouldNotHaveValidationErrorFor(x => x.FrontendBaseUrl);
    }

    [Fact]
    public void Validate_RelativeFrontendBaseUrl_Fails()
    {
        var settings = new OAuthSettings { FrontendBaseUrl = "/not-absolute" };

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.FrontendBaseUrl);
    }

    [Fact]
    public void Validate_EnabledProviderInvalid_MakesSettingsInvalid()
    {
        var settings = new OAuthSettings();
        settings.Providers["google"] = new OidcProviderSettings
        {
            Enabled = true,
            Profile = OidcProviderProfile.Google,
            ClientId = null, // invalid: required when enabled
            ClientSecret = "0123456789abcdef",
            Scopes = "openid;profile;email",
        };

        this.validator.TestValidate(settings).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_DisabledProviderInvalid_IsIgnored()
    {
        var settings = new OAuthSettings();
        settings.Providers["google"] = new OidcProviderSettings
        {
            Enabled = false,
            Profile = OidcProviderProfile.Google,
            ClientId = null, // invalid, but provider is disabled so it is not validated
        };

        this.validator.TestValidate(settings).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EnabledProviderValid_MakesSettingsValid()
    {
        var settings = new OAuthSettings();
        settings.Providers["google"] = new OidcProviderSettings
        {
            Enabled = true,
            Profile = OidcProviderProfile.Google,
            ClientId = "google-client-id",
            ClientSecret = "0123456789abcdef",
            Scopes = "openid;profile;email",
        };

        this.validator.TestValidate(settings).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_DuplicateCallbackPathAcrossEnabledProviders_Fails()
    {
        var settings = new OAuthSettings();
        settings.Providers["google"] = ValidGeneric("https://accounts.google.com");
        settings.Providers["google"].CallbackPath = "/signin-shared";
        settings.Providers["okta"] = ValidGeneric("https://id.example.com");
        settings.Providers["okta"].CallbackPath = "/signin-shared";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Providers);
    }

    [Fact]
    public void Validate_DuplicateDisplayNameAcrossEnabledProviders_Fails()
    {
        var settings = new OAuthSettings();
        settings.Providers["google"] = ValidGeneric("https://accounts.google.com");
        settings.Providers["google"].DisplayName = "Sign in";
        settings.Providers["okta"] = ValidGeneric("https://id.example.com");
        settings.Providers["okta"].DisplayName = "Sign in";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Providers);
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("providers")]
    [InlineData("Providers")] // reserved match is case-insensitive
    public void Validate_ReservedProviderKey_Fails(string reservedKey)
    {
        // Provider keys become the {provider} route segment; "callback"/"providers" collide with the sibling
        // literal routes and would leave the challenge route unreachable, so they are rejected up front.
        var settings = new OAuthSettings();
        settings.Providers[reservedKey] = ValidGeneric("https://id.example.com");

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Providers);
    }

    [Fact]
    public void Validate_DistinctCallbackPathsAndDisplayNames_Passes()
    {
        // Both providers rely on the per-scheme defaults (/signin-oidc/{key} and DisplayName = key),
        // which are inherently unique, so the cross-provider rules do not fire.
        var settings = new OAuthSettings();
        settings.Providers["google"] = ValidGeneric("https://accounts.google.com");
        settings.Providers["okta"] = ValidGeneric("https://id.example.com");

        this.validator.TestValidate(settings).IsValid.ShouldBeTrue();
    }

    private static OidcProviderSettings ValidGeneric(string authority) => new()
    {
        Enabled = true,
        Profile = OidcProviderProfile.Generic,
        Authority = authority,
        ClientId = "client-id",
        ClientSecret = "0123456789abcdef",
        Scopes = "openid;profile;email",
    };
}
