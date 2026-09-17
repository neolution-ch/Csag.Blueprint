namespace Csag.Blueprint.Web.UnitTests.Options.Localization;

using Csag.Blueprint.Web.Options.Localization;
using FluentValidation.TestHelper;

/// <summary>
/// Unit tests for <see cref="LocalizationOptionsValidator"/>, covering the default language,
/// supported languages list, the containment rule between them, and the L1 translation cache
/// expiration.
/// </summary>
public sealed class LocalizationOptionsValidatorTests
{
    private readonly LocalizationOptionsValidator validator = new();

    [Fact]
    public void Validate_ValidOptions_Passes()
    {
        var options = CreateValidOptions();

        var result = this.validator.TestValidate(options);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyDefaultLanguage_Fails()
    {
        var options = CreateValidOptions();
        options.DefaultLanguage = string.Empty;

        var result = this.validator.TestValidate(options);

        result.ShouldHaveValidationErrorFor(x => x.DefaultLanguage)
            .WithErrorMessage("Default language must be specified");
    }

    [Fact]
    public void Validate_EmptySupportedLanguages_Fails()
    {
        var options = CreateValidOptions();
        options.SupportedLanguages = new List<string>();

        var result = this.validator.TestValidate(options);

        result.ShouldHaveValidationErrorFor(x => x.SupportedLanguages)
            .WithErrorMessage("At least one supported language must be specified");
    }

    [Fact]
    public void Validate_DefaultLanguageNotInSupportedLanguages_Fails()
    {
        var options = CreateValidOptions();
        options.DefaultLanguage = "fr";

        var result = this.validator.TestValidate(options);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "Default language must be included in the supported languages list");
    }

    [Fact]
    public void Validate_CaseMismatchedDefaultLanguage_Fails()
    {
        // The containment check uses ordinal, case-sensitive comparison.
        var options = CreateValidOptions();
        options.DefaultLanguage = "EN";

        var result = this.validator.TestValidate(options);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "Default language must be included in the supported languages list");
    }

    [Fact]
    public void Validate_NullSupportedLanguages_FailsWithNotEmptyError()
    {
        // A null list is a validation failure in its own right; the containment rule is skipped
        // because there is no list to search.
        var options = CreateValidOptions();
        options.SupportedLanguages = null!;

        var result = this.validator.TestValidate(options);

        result.ShouldHaveValidationErrorFor(x => x.SupportedLanguages)
            .WithErrorMessage("At least one supported language must be specified");
        result.Errors.ShouldNotContain(error => error.ErrorMessage == "Default language must be included in the supported languages list");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveTranslationCacheL1Expiration_Fails(int expirationMinutes)
    {
        var options = CreateValidOptions();
        options.TranslationCacheL1ExpirationMinutes = expirationMinutes;

        var result = this.validator.TestValidate(options);

        result.ShouldHaveValidationErrorFor(x => x.TranslationCacheL1ExpirationMinutes)
            .WithErrorMessage("TranslationCacheL1ExpirationMinutes must be greater than 0");
    }

    private static LocalizationOptions CreateValidOptions()
    {
        return new LocalizationOptions
        {
            DefaultLanguage = "en",
            SupportedLanguages = new List<string> { "en", "de" },
            TranslationCacheL1ExpirationMinutes = 5,
        };
    }
}
