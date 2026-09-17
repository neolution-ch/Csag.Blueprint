namespace Csag.Blueprint.TestHost.Localization;

/// <summary>
/// Translation key constants for the host's localizable strings. Each key must have an entry in
/// <see cref="TranslationDefaults.All"/>; the database localizer only resolves keys that are part
/// of that code-owned catalog.
/// </summary>
internal static class TranslationKeys
{
    /// <summary>
    /// Greeting that is seeded with database values in both supported languages.
    /// </summary>
    internal const string GreetingHello = "Greeting.Hello";

    /// <summary>
    /// Greeting that is seeded with a database value only in the default language, so requests in
    /// other languages exercise the fallback to the default-language database row.
    /// </summary>
    internal const string GreetingEnglishOnly = "Greeting.EnglishOnly";

    /// <summary>
    /// Greeting that is never seeded into the database, so every request exercises the final
    /// fallback to the code-defined default text.
    /// </summary>
    internal const string GreetingCodeOnly = "Greeting.CodeOnly";
}
