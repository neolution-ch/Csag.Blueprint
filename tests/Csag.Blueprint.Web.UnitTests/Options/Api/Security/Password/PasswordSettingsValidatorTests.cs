namespace Csag.Blueprint.Web.UnitTests.Options.Api.Security.Password;

using Csag.Blueprint.Web.Options.Api.Security.Password;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="PasswordSettingsValidator"/>, covering the length/unique-chars
/// bounds and the relational rule between them.
/// </summary>
public sealed class PasswordSettingsValidatorTests
{
    private readonly PasswordSettingsValidator validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 4)]
    [InlineData(256, 256)]
    public void Validate_ValidLengthCombinations_Pass(int requiredLength, int requiredUniqueChars)
    {
        var settings = new PasswordSettings
        {
            RequiredLength = requiredLength,
            RequiredUniqueChars = requiredUniqueChars,
        };

        var result = this.validator.TestValidate(settings);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RequiredLengthBelowOne_Fails(int requiredLength)
    {
        var settings = new PasswordSettings { RequiredLength = requiredLength, RequiredUniqueChars = 1 };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequiredLength)
            .WithErrorMessage("RequiredLength must be at least 1");
    }

    [Fact]
    public void Validate_RequiredLengthAbove256_Fails()
    {
        var settings = new PasswordSettings { RequiredLength = 257, RequiredUniqueChars = 1 };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequiredLength)
            .WithErrorMessage("RequiredLength must not exceed 256");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RequiredUniqueCharsBelowOne_Fails(int requiredUniqueChars)
    {
        var settings = new PasswordSettings { RequiredLength = 8, RequiredUniqueChars = requiredUniqueChars };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequiredUniqueChars)
            .WithErrorMessage("RequiredUniqueChars must be at least 1");
    }

    [Fact]
    public void Validate_RequiredUniqueCharsAbove256_Fails()
    {
        var settings = new PasswordSettings { RequiredLength = 256, RequiredUniqueChars = 257 };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequiredUniqueChars)
            .WithErrorMessage("RequiredUniqueChars must not exceed 256");
    }

    [Fact]
    public void Validate_RequiredUniqueCharsGreaterThanRequiredLength_Fails()
    {
        var settings = new PasswordSettings { RequiredLength = 8, RequiredUniqueChars = 9 };

        var result = this.validator.TestValidate(settings);

        result.ShouldHaveValidationErrorFor(x => x.RequiredUniqueChars)
            .WithErrorMessage("RequiredUniqueChars cannot be greater than RequiredLength");
    }
}
