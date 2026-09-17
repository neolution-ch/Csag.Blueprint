namespace Csag.Blueprint.TestHost.Endpoints.Localization.Greeting;

/// <summary>
/// The localized greeting strings resolved for the request culture, one per localization
/// fallback tier.
/// </summary>
public sealed class GreetingResponse
{
    /// <summary>
    /// Gets or sets the UI culture the request was resolved to.
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the greeting that has database rows in every supported language.
    /// </summary>
    public string Hello { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the greeting that has a database row only in the default language.
    /// </summary>
    public string EnglishOnly { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the greeting that has no database rows and always resolves to the code default.
    /// </summary>
    public string CodeOnly { get; set; } = string.Empty;
}
