namespace Csag.Blueprint.TestHost.Localization;

/// <summary>
/// Code-owned catalog of translation keys with their default-language text. The database
/// localizer merges these defaults with the <c>BlueprintTranslations</c> rows: a row in the
/// requested language wins, then a row in the default language, then the text defined here.
/// </summary>
public static class TranslationDefaults
{
    /// <summary>
    /// Gets all translation keys with their code-defined default text.
    /// </summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TranslationKeys.GreetingHello] = "Hello (code default)",
        [TranslationKeys.GreetingEnglishOnly] = "English-only (code default)",
        [TranslationKeys.GreetingCodeOnly] = "Code-only greeting",
    };
}
