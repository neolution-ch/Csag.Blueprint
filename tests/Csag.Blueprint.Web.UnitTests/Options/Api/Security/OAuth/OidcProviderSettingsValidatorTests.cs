namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.OAuth;

using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for the profile-aware <see cref="OidcProviderSettingsValidator"/>.
/// </summary>
public sealed class OidcProviderSettingsValidatorTests
{
    private readonly OidcProviderSettingsValidator validator = new();

    [Fact]
    public void Validate_ValidGoogleProvider_Passes()
    {
        var result = this.validator.TestValidate(ValidGoogle());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingClientId_Fails()
    {
        var settings = ValidGoogle();
        settings.ClientId = null;

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void Validate_ShortClientSecret_Fails()
    {
        var settings = ValidGoogle();
        settings.ClientSecret = "tooshort";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.ClientSecret);
    }

    [Fact]
    public void Validate_ScopesMissingOpenid_Fails()
    {
        var settings = ValidGoogle();
        settings.Scopes = "profile;email";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Scopes);
    }

    [Fact]
    public void Validate_ScopesUsingCommaDelimiter_Fails()
    {
        var settings = ValidGoogle();
        settings.Scopes = "openid,profile,email";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Scopes);
    }

    [Fact]
    public void Validate_GenericProfileWithoutAuthority_Fails()
    {
        var settings = ValidGoogle();
        settings.Profile = OidcProviderProfile.Generic;
        settings.Authority = null;

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Authority);
    }

    [Fact]
    public void Validate_GenericProfileWithHttpsAuthority_Passes()
    {
        var settings = ValidGoogle();
        settings.Profile = OidcProviderProfile.Generic;
        settings.Authority = "https://id.example.com";

        this.validator.TestValidate(settings).ShouldNotHaveValidationErrorFor(x => x.Authority);
    }

    [Fact]
    public void Validate_NonHttpsAuthority_Fails()
    {
        var settings = ValidGoogle();
        settings.Profile = OidcProviderProfile.Generic;
        settings.Authority = "http://insecure.example.com";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.Authority);
    }

    [Fact]
    public void Validate_EntraSingleTenantWithoutTenantId_Fails()
    {
        var settings = ValidEntra(MicrosoftEntraSignInAudience.SingleTenant);
        settings.TenantId = null;

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Validate_EntraSingleTenantWithInvalidTenantId_Fails()
    {
        var settings = ValidEntra(MicrosoftEntraSignInAudience.SingleTenant);
        settings.TenantId = "not-a-guid";

        this.validator.TestValidate(settings).ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Validate_EntraSingleTenantWithValidTenantId_Passes()
    {
        var settings = ValidEntra(MicrosoftEntraSignInAudience.SingleTenant);
        settings.TenantId = "11111111-1111-1111-1111-111111111111";

        this.validator.TestValidate(settings).ShouldNotHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Validate_EntraMultiTenantWithoutTenantId_Passes()
    {
        var settings = ValidEntra(MicrosoftEntraSignInAudience.MultiTenant);
        settings.TenantId = null;

        this.validator.TestValidate(settings).ShouldNotHaveValidationErrorFor(x => x.TenantId);
    }

    private static OidcProviderSettings ValidGoogle() => new()
    {
        Enabled = true,
        Profile = OidcProviderProfile.Google,
        ClientId = "google-client-id",
        ClientSecret = "0123456789abcdef",
        Scopes = "openid;profile;email",
    };

    private static OidcProviderSettings ValidEntra(MicrosoftEntraSignInAudience audience) => new()
    {
        Enabled = true,
        Profile = OidcProviderProfile.Entra,
        ClientId = "entra-client-id",
        ClientSecret = "0123456789abcdef",
        Scopes = "openid;profile;email",
        SignInAudience = audience,
    };
}
