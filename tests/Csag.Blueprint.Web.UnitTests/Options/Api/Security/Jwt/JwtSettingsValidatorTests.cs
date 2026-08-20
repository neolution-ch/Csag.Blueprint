namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.Jwt;

using Csag.Blueprint.Web.Options.Api.Security.Jwt;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="JwtSettingsValidator"/>, covering issuer, audience, expiration, and signing key rules.
/// </summary>
public sealed class JwtSettingsValidatorTests
{
    private readonly JwtSettingsValidator validator = new();

    [Fact]
    public void Validate_ValidSettings_Passes()
    {
        var settings = CreateValidSettings();

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyIssuer_Fails()
    {
        var settings = CreateValidSettings();
        settings.Issuer = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Issuer)
            .WithErrorMessage("JWT issuer must not be empty.");
    }

    [Fact]
    public void Validate_EmptyAudience_Fails()
    {
        var settings = CreateValidSettings();
        settings.Audience = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Audience)
            .WithErrorMessage("JWT audience must not be empty.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveExpirationHours_Fails(int expirationHours)
    {
        var settings = CreateValidSettings();
        settings.ExpirationHours = expirationHours;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.ExpirationHours)
            .WithErrorMessage("JWT token expiration must be greater than 0 hours.");
    }

    [Fact]
    public void Validate_EmptySigningKey_Fails()
    {
        // Despite the validator's XML doc claiming the signing key is validated only in Program.cs,
        // the rule set does enforce NotEmpty + MinimumLength(32) here; this pins that behavior.
        var settings = CreateValidSettings();
        settings.SigningKey = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.SigningKey);
    }

    [Fact]
    public void Validate_SigningKeyShorterThan32Chars_Fails()
    {
        var settings = CreateValidSettings();
        settings.SigningKey = new string('k', 31);

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.SigningKey)
            .WithErrorMessage("JWT signing key must be at least 32 characters long for HS256 security.");
    }

    [Fact]
    public void Validate_SigningKeyExactly32Chars_Passes()
    {
        var settings = CreateValidSettings();
        settings.SigningKey = new string('k', 32);

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveValidationErrorFor(x => x.SigningKey);
    }

    private static JwtSettings CreateValidSettings()
    {
        return new JwtSettings
        {
            SigningKey = new string('k', 64),
            Issuer = "csag-blueprint",
            Audience = "csag-blueprint-api",
            ExpirationHours = 8,
        };
    }
}
