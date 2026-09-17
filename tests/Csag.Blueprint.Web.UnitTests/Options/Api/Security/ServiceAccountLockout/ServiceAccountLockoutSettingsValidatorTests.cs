namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.ServiceAccountLockout;

using Csag.Blueprint.Web.Options.Api.Security.ServiceAccountLockout;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="ServiceAccountLockoutSettingsValidator"/>, covering the
/// failed-attempt threshold and lockout duration bounds.
/// </summary>
public sealed class ServiceAccountLockoutSettingsValidatorTests
{
    private readonly ServiceAccountLockoutSettingsValidator validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 15)]
    [InlineData(100, 1440)]
    public void Validate_ValuesWithinBounds_Pass(int maxFailedAccessAttempts, int lockoutDurationMinutes)
    {
        var settings = new ServiceAccountLockoutSettings
        {
            MaxFailedAccessAttempts = maxFailedAccessAttempts,
            LockoutDurationMinutes = lockoutDurationMinutes,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxFailedAccessAttemptsBelowOne_Fails(int maxFailedAccessAttempts)
    {
        var settings = new ServiceAccountLockoutSettings
        {
            MaxFailedAccessAttempts = maxFailedAccessAttempts,
            LockoutDurationMinutes = 15,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.MaxFailedAccessAttempts)
            .WithErrorMessage("MaxFailedAccessAttempts must be at least 1");
    }

    [Fact]
    public void Validate_MaxFailedAccessAttemptsAbove100_Fails()
    {
        var settings = new ServiceAccountLockoutSettings
        {
            MaxFailedAccessAttempts = 101,
            LockoutDurationMinutes = 15,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.MaxFailedAccessAttempts)
            .WithErrorMessage("MaxFailedAccessAttempts must not exceed 100");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_LockoutDurationBelowOne_Fails(int lockoutDurationMinutes)
    {
        var settings = new ServiceAccountLockoutSettings
        {
            MaxFailedAccessAttempts = 5,
            LockoutDurationMinutes = lockoutDurationMinutes,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.LockoutDurationMinutes)
            .WithErrorMessage("LockoutDurationMinutes must be at least 1");
    }

    [Fact]
    public void Validate_LockoutDurationAboveOneDay_Fails()
    {
        var settings = new ServiceAccountLockoutSettings
        {
            MaxFailedAccessAttempts = 5,
            LockoutDurationMinutes = 1441,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.LockoutDurationMinutes)
            .WithErrorMessage("LockoutDurationMinutes must not exceed 1440 (24 hours)");
    }
}
