namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security;

using Csag.Blueprint.Web.Options.Api.Security;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="MfaSettingsValidator"/>, enforcing that MFA can only be required when supported.
/// </summary>
public sealed class MfaSettingsValidatorTests
{
    private readonly MfaSettingsValidator validator = new();

    [Theory]
    [InlineData(false, false)] // not supported, not required
    [InlineData(true, false)] // supported, optional
    [InlineData(true, true)] // supported, required
    public void Validate_ValidCombinations_Pass(bool enabled, bool required)
    {
        var settings = new MfaSettings
        {
            Enabled = enabled,
            Required = required,
            RecoveryCodeCount = 10,
            Issuer = "CSAG Blueprint",
            BypassExternalProviders = new List<string>(),
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RequiredWithoutEnabled_Fails()
    {
        var settings = new MfaSettings { Enabled = false, Required = true };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Required)
            .WithErrorMessage("Mfa.Required can only be true when Mfa.Enabled is also true.");
    }

    [Fact]
    public void Validate_NullBypassList_Fails()
    {
        var settings = new MfaSettings { BypassExternalProviders = null! };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.BypassExternalProviders);
    }

    [Fact]
    public void Validate_EmptyIssuerWhenEnabled_Fails()
    {
        var settings = new MfaSettings
        {
            Enabled = true,
            RecoveryCodeCount = 10,
            BypassExternalProviders = new List<string>(),
            Issuer = string.Empty,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.Issuer)
            .WithErrorMessage("Mfa.Issuer must not be empty when Mfa.Enabled is true.");
    }

    [Fact]
    public void Validate_EmptyIssuerWhenDisabled_Passes()
    {
        // The issuer is only used to build otpauth URIs, so it is irrelevant while MFA is disabled.
        var settings = new MfaSettings { Enabled = false, Issuer = string.Empty };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveValidationErrorFor(x => x.Issuer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveRecoveryCodeCountWhenEnabled_Fails(int recoveryCodeCount)
    {
        var settings = new MfaSettings { Enabled = true, RecoveryCodeCount = recoveryCodeCount };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RecoveryCodeCount)
            .WithErrorMessage("Mfa.RecoveryCodeCount must be greater than 0 when Mfa.Enabled is true.");
    }

    [Fact]
    public void Validate_NonPositiveRecoveryCodeCountWhenDisabled_Passes()
    {
        // The recovery code count is irrelevant while MFA is disabled, so it is not validated.
        var settings = new MfaSettings { Enabled = false, RecoveryCodeCount = 0 };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveValidationErrorFor(x => x.RecoveryCodeCount);
    }
}
