namespace Csag.Blueprint.Web.UnitTests.Validation;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Web.Validation;
using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Localization;

/// <summary>
/// Unit tests for <see cref="CountryCodeRuleBuilderExtensions.MustBeCountryCode{T}"/>.
/// </summary>
public sealed class CountryCodeRuleBuilderExtensionsTests
{
    private const string FieldKey = "Fields.Nationality";

    private readonly CountryModelValidator validator = new(new FixedCountryCatalog(), new FieldNameStringLocalizer(), FieldKey);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CH")]
    [InlineData("DE")]
    public void Validate_EmptyOrAcceptedCode_Passes(string? code)
    {
        var result = this.validator.TestValidate(new CountryModel { Country = code });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("ch")]
    [InlineData(" ")]
    public void Validate_CodeNotInCatalog_FailsWithLocalizedMessage(string code)
    {
        var result = this.validator.TestValidate(new CountryModel { Country = code });

        result.ShouldHaveValidationErrorFor(x => x.Country)
            .WithErrorMessage("Validation.InvalidCountry: Fields.Nationality");
    }

    [Fact]
    public void MustBeCountryCode_NullCatalog_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new CountryModelValidator(null!, new FieldNameStringLocalizer(), FieldKey));
    }

    [Fact]
    public void MustBeCountryCode_NullLocalizer_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new CountryModelValidator(new FixedCountryCatalog(), null!, FieldKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MustBeCountryCode_MissingFieldKey_Throws(string fieldKey)
    {
        Should.Throw<ArgumentException>(() => new CountryModelValidator(new FixedCountryCatalog(), new FieldNameStringLocalizer(), fieldKey));
    }

    private sealed class CountryModel
    {
        public string? Country { get; set; }
    }

    private sealed class CountryModelValidator : AbstractValidator<CountryModel>
    {
        public CountryModelValidator(ICountryCatalog countries, IStringLocalizer localizer, string fieldKey)
        {
            this.RuleFor(x => x.Country).MustBeCountryCode(countries, localizer, fieldKey);
        }
    }

    /// <summary>
    /// Catalog stub that contains only <c>CH</c> and <c>DE</c>, so that the tests do not depend on a
    /// country data library.
    /// </summary>
    private sealed class FixedCountryCatalog : ICountryCatalog
    {
        public IReadOnlyList<string> Codes { get; } = ["CH", "DE"];

        public bool Contains(string? code) => this.Codes.Contains(code, StringComparer.Ordinal);
    }

    /// <summary>
    /// Localizer stub that echoes the lookup key and appends the <c>FieldName</c> argument, so that a
    /// test can see both the message key and the field label in the validation message.
    /// </summary>
    private sealed class FieldNameStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var fieldName = arguments.Length > 0
                    ? arguments[0].GetType().GetProperty("FieldName")?.GetValue(arguments[0])
                    : null;
                return new(name, $"{name}: {fieldName}");
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
