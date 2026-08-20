namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.Jwt;

using Csag.Blueprint.Web.Options.Api.Security.Jwt;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="JwtSettingsValidator"/>, covering issuer, audience, expiration, and the
/// signing key strength rule (key presence is deferred to the host's generation-mode-gated check).
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
    public void Validate_EmptySigningKey_Passes()
    {
        // Key presence is deferred to the host's generation-mode-gated startup check: this validator
        // runs unconditionally, including generation mode where no signing key is configured, so an
        // absent key must not fail options validation.
        var settings = CreateValidSettings();
        settings.SigningKey = string.Empty;

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveValidationErrorFor(x => x.SigningKey);
    }

    [Fact]
    public void Validate_NullSigningKey_Passes()
    {
        // Binding leaves the key null when the configuration section omits it entirely; like the
        // empty string, that is the generation-mode shape and must pass.
        var settings = CreateValidSettings();
        settings.SigningKey = null!;

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveValidationErrorFor(x => x.SigningKey);
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
