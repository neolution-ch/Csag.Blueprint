namespace Csag.Blueprint.Web.Validation;

using FluentValidation;
using Microsoft.Extensions.Localization;

/// <summary>
/// Helper extensions for resolving localized FluentValidation messages lazily at validation time.
/// </summary>
public static class LocalizedValidationExtensions
{
    /// <summary>
    /// Resolves a localized message without format arguments when validation executes.
    /// </summary>
    /// <typeparam name="T">The model type being validated.</typeparam>
    /// <typeparam name="TProperty">The property type being validated.</typeparam>
    /// <param name="rule">The validation rule.</param>
    /// <param name="localizer">The localizer used to resolve the translation key.</param>
    /// <param name="translationKey">The translation key or default text.</param>
    /// <returns>The same rule for chaining.</returns>
    public static IRuleBuilderOptions<T, TProperty> WithLocalizedMessage<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        IStringLocalizer localizer,
        string translationKey)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(translationKey);

        return rule.WithMessage(_ => localizer[translationKey]);
    }

    /// <summary>
    /// Resolves a formatted localized message lazily when validation executes.
    /// </summary>
    /// <typeparam name="T">The model type being validated.</typeparam>
    /// <typeparam name="TProperty">The property type being validated.</typeparam>
    /// <param name="rule">The validation rule.</param>
    /// <param name="localizer">The localizer used to resolve the translation key.</param>
    /// <param name="translationKey">The translation key or default text.</param>
    /// <param name="argumentsFactory">Factory for placeholder arguments evaluated per request.</param>
    /// <returns>The same rule for chaining.</returns>
    public static IRuleBuilderOptions<T, TProperty> WithLocalizedMessage<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        IStringLocalizer localizer,
        string translationKey,
        Func<object> argumentsFactory)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(translationKey);
        ArgumentNullException.ThrowIfNull(argumentsFactory);

        return rule.WithMessage(_ =>
        {
            var arguments = argumentsFactory();
            return localizer[translationKey, arguments];
        });
    }
}
