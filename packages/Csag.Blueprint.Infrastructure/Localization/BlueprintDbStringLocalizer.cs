namespace Csag.Blueprint.Infrastructure.Localization;

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Custom <see cref="IStringLocalizer"/> implementation backed by <see cref="ITranslationProvider"/>.
/// </summary>
/// <remarks>
/// Lookup flow:
/// 1. The input name is a dot-separated key path (e.g. "Validation.EmailRequired") from <c>TranslationKeys</c>.
/// 2. Look up the key directly in the merged translation snapshot for the current UI culture.
/// 3. Fall back to the English default text from <c>TranslationDefaults.All</c> if not found in the DB.
/// </remarks>
public sealed class BlueprintDbStringLocalizer : IStringLocalizer
{
    private readonly ITranslationProvider translationProvider;
    private readonly ILogger<BlueprintDbStringLocalizer> logger;
    private readonly IReadOnlyDictionary<string, string> translationDefaults;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueprintDbStringLocalizer"/> class.
    /// </summary>
    /// <param name="translationProvider">The translation provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="translationDefaults">Dictionary mapping dot-path keys to English default text (i.e. <c>TranslationDefaults.All</c>).</param>
    public BlueprintDbStringLocalizer(
        ITranslationProvider translationProvider,
        ILogger<BlueprintDbStringLocalizer> logger,
        IReadOnlyDictionary<string, string> translationDefaults)
    {
        this.translationProvider = translationProvider;
        this.logger = logger;
        this.translationDefaults = translationDefaults;
    }

    /// <inheritdoc/>
    public LocalizedString this[string name]
    {
        get
        {
            var (value, resourceNotFound) = this.GetTranslation(name);
            return new LocalizedString(name, value, resourceNotFound);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Supports two placeholder styles:
    /// <list type="bullet">
    ///   <item><description>Named placeholders with anonymous objects: <c>localizer["Hello {Name}", new { Name = "World" }]</c></description></item>
    ///   <item><description>Positional placeholders: <c>localizer["Hello {0}", "World"]</c></description></item>
    /// </list>
    /// </remarks>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var (value, resourceNotFound) = this.GetTranslation(name);
            var formatted = FormatValue(value, arguments);
            return new LocalizedString(name, formatted, resourceNotFound);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var snapshot = this.translationProvider.GetTranslations(culture);

        foreach (var kvp in snapshot.Translations)
        {
            yield return new LocalizedString(kvp.Key, kvp.Value, false);
        }
    }

    /// <summary>
    /// Formats a translated value by replacing named placeholders (<c>{Name}</c>) with values
    /// from an anonymous object, or falling back to positional <see cref="string.Format(IFormatProvider, string, object[])"/>.
    /// </summary>
    /// <param name="value">The translated string potentially containing placeholders.</param>
    /// <param name="arguments">Either a single anonymous object for named placeholders, or positional arguments.</param>
    /// <returns>The formatted string.</returns>
    private static string FormatValue(string value, object[] arguments)
    {
        if (arguments.Length == 0)
        {
            return value;
        }

        // If the first argument is an anonymous type (or any complex object), try named replacement
        var firstArg = arguments[0];
        if (firstArg != null && IsNamedPlaceholderObject(firstArg))
        {
            return ReplaceNamedPlaceholders(value, firstArg);
        }

        // Fall back to positional string.Format
        return string.Format(CultureInfo.CurrentCulture, value, arguments);
    }

    /// <summary>
    /// Determines whether the argument is an anonymous or complex object intended for named placeholder replacement
    /// (as opposed to a primitive/string used for positional formatting).
    /// </summary>
    /// <param name="obj">The argument to check.</param>
    /// <returns>True if the object should be used for named placeholder replacement.</returns>
    private static bool IsNamedPlaceholderObject(object obj)
    {
        var type = obj.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        {
            return false;
        }

        return type != typeof(DateTime)
            && type != typeof(DateTimeOffset)
            && type != typeof(Guid);
    }

    /// <summary>
    /// Replaces <c>{PropertyName}</c> placeholders in the value with the corresponding property values from the source object.
    /// </summary>
    /// <param name="value">The string containing named placeholders.</param>
    /// <param name="source">The object whose properties supply the replacement values.</param>
    /// <returns>The string with placeholders replaced.</returns>
    private static string ReplaceNamedPlaceholders(string value, object source)
    {
        var properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propertyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in properties)
        {
            var propValue = prop.GetValue(source);
            propertyMap[prop.Name] = propValue?.ToString() ?? string.Empty;
        }

        return Regex.Replace(value, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            return propertyMap.TryGetValue(key, out var replacement) ? replacement : match.Value;
        });
    }

    private (string Value, bool ResourceNotFound) GetTranslation(string name)
    {
        // name is a dot-separated key path, e.g. "Validation.EmailRequired"
        var culture = CultureInfo.CurrentUICulture.Name;

        // The provider returns merged translations with all fallbacks already applied
        var snapshot = this.translationProvider.GetTranslations(culture);
        if (snapshot.Translations.TryGetValue(name, out var value))
        {
            return (value, false);
        }

        // Key not found in DB — fall back to the English default text
        var fallback = this.translationDefaults.TryGetValue(name, out var english) ? english : name;
        this.logger.LogDebug("Translation key not found: '{Key}' in culture '{Culture}'", name, culture);
        return (fallback, true);
    }
}
