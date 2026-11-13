namespace Csag.Blueprint.Web.Validation;

using Csag.Blueprint.Application.Abstractions.Services;
using FluentValidation;
using Microsoft.Extensions.Localization;

/// <summary>
/// Extension methods for country code validation.
/// </summary>
public static class CountryCodeRuleBuilderExtensions
{
    /// <summary>
    /// Translation key path for the invalid country message.
    /// Corresponds to <c>TranslationKeys.Validation.InvalidCountry</c>.
    /// </summary>
    private const string InvalidCountryKey = "Validation.InvalidCountry";

    /// <summary>
    /// Requires a country code from <paramref name="countries"/>.
    /// </summary>
    /// <remarks>
    /// A null or empty value passes, so the rule fits an optional field. Add <c>NotEmpty()</c>
    /// when the field is required. The application defines the text of the
    /// <c>Validation.InvalidCountry</c> key with a <c>{FieldName}</c> placeholder.
    /// </remarks>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="rule">The rule builder for the country code field.</param>
    /// <param name="countries">The catalog of accepted country codes.</param>
    /// <param name="localizer">The string localizer for the validation message.</param>
    /// <param name="fieldKey">The translation key path for the field label, for example <c>TranslationKeys.Fields.Nationality</c>.</param>
    /// <returns>The rule builder for continued chaining.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeCountryCode<T>(
        this IRuleBuilder<T, string?> rule,
        ICountryCatalog countries,
        IStringLocalizer localizer,
        string fieldKey)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);

        return rule
            .Must(code => string.IsNullOrEmpty(code) || countries.Contains(code))
            .WithLocalizedMessage(localizer, InvalidCountryKey, () => new { FieldName = localizer[fieldKey] });
    }
}
