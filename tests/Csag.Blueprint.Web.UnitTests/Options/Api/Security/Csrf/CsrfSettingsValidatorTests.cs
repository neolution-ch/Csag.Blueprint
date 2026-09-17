namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.Csrf;

using Csag.Blueprint.Web.Options.Api.Security.Csrf;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="CsrfSettingsValidator"/>, covering the header/cookie name rules
/// and the requirement that the two cookies use distinct names.
/// </summary>
public sealed class CsrfSettingsValidatorTests
{
    private readonly CsrfSettingsValidator validator = new();

    [Fact]
    public void Validate_ValidSettings_Passes()
    {
        var settings = CreateValidSettings();

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyHeaderName_Fails()
    {
        var settings = CreateValidSettings();
        settings.HeaderName = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.HeaderName)
            .WithErrorMessage("CSRF header name must not be empty.");
    }

    [Fact]
    public void Validate_EmptyCookieName_Fails()
    {
        var settings = CreateValidSettings();
        settings.CookieName = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.CookieName)
            .WithErrorMessage("CSRF cookie name must not be empty.");
    }

    [Fact]
    public void Validate_EmptyRequestTokenCookieName_Fails()
    {
        var settings = CreateValidSettings();
        settings.RequestTokenCookieName = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequestTokenCookieName)
            .WithErrorMessage("CSRF request token cookie name must not be empty.");
    }

    [Fact]
    public void Validate_SameCookieAndRequestTokenCookieName_Fails()
    {
        var settings = CreateValidSettings();
        settings.CookieName = "csrf-cookie";
        settings.RequestTokenCookieName = "csrf-cookie";

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.CookieName)
            .WithErrorMessage("CSRF cookie name and request token cookie name must be different.");
    }

    [Fact]
    public void Validate_DisabledSettingsWithEmptyNames_StillFails()
    {
        // The name rules are not gated on Enabled, so even a disabled CSRF configuration
        // must carry complete cookie/header names.
        var settings = new CsrfSettings
        {
            Enabled = false,
            HeaderName = string.Empty,
            CookieName = string.Empty,
            RequestTokenCookieName = string.Empty,
        };

        var result = this.validator.TestValidate(settings);

        result.IsValid.ShouldBeFalse();
    }

    private static CsrfSettings CreateValidSettings()
    {
        return new CsrfSettings
        {
            Enabled = true,
            HeaderName = "X-CSRF-TOKEN",
            CookieName = ".Blueprint.Antiforgery",
            RequestTokenCookieName = "XSRF-REQUEST-TOKEN",
        };
    }
}
