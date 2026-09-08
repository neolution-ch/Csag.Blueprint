namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.PasswordReset;

using Csag.Blueprint.Web.Options.Api.Security.PasswordReset;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="PasswordResetSettingsValidator"/>, covering the token lifetime bounds.
/// </summary>
public sealed class PasswordResetSettingsValidatorTests
{
    private readonly PasswordResetSettingsValidator validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(1440)]
    public void Validate_LifetimeWithinBounds_Passes(int tokenLifetimeMinutes)
    {
        var settings = new PasswordResetSettings { TokenLifetimeMinutes = tokenLifetimeMinutes };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_LifetimeBelowOne_Fails(int tokenLifetimeMinutes)
    {
        var settings = new PasswordResetSettings { TokenLifetimeMinutes = tokenLifetimeMinutes };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.TokenLifetimeMinutes)
            .WithErrorMessage("TokenLifetimeMinutes must be at least 1");
    }

    [Fact]
    public void Validate_LifetimeAboveOneDay_Fails()
    {
        var settings = new PasswordResetSettings { TokenLifetimeMinutes = 1441 };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.TokenLifetimeMinutes)
            .WithErrorMessage("TokenLifetimeMinutes must not exceed 1440 (24 hours)");
    }
}
