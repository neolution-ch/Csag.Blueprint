namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.Cors;

using Csag.Blueprint.Web.Options.Api.Security.Cors;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="CorsSettingsValidator"/>, covering origin format checks,
/// the credentials/origins interplay, and the preflight max-age bounds.
/// </summary>
public sealed class CorsSettingsValidatorTests
{
    private readonly CorsSettingsValidator validator = new();

    [Fact]
    public void Validate_ValidSettings_Passes()
    {
        var settings = CreateValidSettings();

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingOriginsWithoutCredentials_Passes(string? allowedOrigins)
    {
        var settings = CreateValidSettings();
        settings.AllowCredentials = false;
        settings.AllowedOrigins = allowedOrigins;

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")] // only http/https schemes are accepted
    [InlineData("https://example.com/")] // trailing slash is rejected
    [InlineData("https://good.example.com;bad url")] // a single bad entry fails the whole list
    public void Validate_InvalidOrigin_Fails(string allowedOrigins)
    {
        var settings = CreateValidSettings();
        settings.AllowCredentials = false;
        settings.AllowedOrigins = allowedOrigins;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.AllowedOrigins)
            .WithErrorMessage("AllowedOrigins must contain valid URLs without trailing slashes, separated by semicolons");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:20023;https://app.example.com")]
    [InlineData("https://app.example.com; https://other.example.com")] // entries are trimmed before parsing
    public void Validate_ValidOrigins_Pass(string allowedOrigins)
    {
        var settings = CreateValidSettings();
        settings.AllowCredentials = false;
        settings.AllowedOrigins = allowedOrigins;

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_CredentialsWithoutOrigins_Fails(string? allowedOrigins)
    {
        var settings = CreateValidSettings();
        settings.AllowCredentials = true;
        settings.AllowedOrigins = allowedOrigins;

        var result = this.validator.TestValidate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "AllowCredentials cannot be true when AllowedOrigins is null or empty. Specify explicit origins for security.");
    }

    [Fact]
    public void Validate_CredentialsWithWildcardOrigin_Fails()
    {
        var settings = CreateValidSettings();
        settings.AllowCredentials = true;
        settings.AllowedOrigins = "*";

        var result = this.validator.TestValidate(settings);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "AllowCredentials cannot be true when AllowedOrigins contains wildcard '*'. This is a security risk. Specify explicit origins.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositivePreflightMaxAge_Fails(int preflightMaxAgeSeconds)
    {
        var settings = CreateValidSettings();
        settings.PreflightMaxAgeSeconds = preflightMaxAgeSeconds;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.PreflightMaxAgeSeconds)
            .WithErrorMessage("PreflightMaxAgeSeconds must be greater than 0");
    }

    [Fact]
    public void Validate_PreflightMaxAgeAboveOneDay_Fails()
    {
        var settings = CreateValidSettings();
        settings.PreflightMaxAgeSeconds = 86401;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.PreflightMaxAgeSeconds)
            .WithErrorMessage("PreflightMaxAgeSeconds cannot exceed 86400 seconds (24 hours)");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(86400)]
    public void Validate_PreflightMaxAgeBoundaries_Pass(int preflightMaxAgeSeconds)
    {
        var settings = CreateValidSettings();
        settings.PreflightMaxAgeSeconds = preflightMaxAgeSeconds;

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveValidationErrorFor(x => x.PreflightMaxAgeSeconds);
    }

    private static CorsSettings CreateValidSettings()
    {
        return new CorsSettings
        {
            AllowedOrigins = "https://app.example.com",
            AllowCredentials = true,
            AllowedMethods = "GET;POST",
            AllowedHeaders = "Content-Type;Authorization",
            ExposedHeaders = null,
            PreflightMaxAgeSeconds = 600,
        };
    }
}
