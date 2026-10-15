namespace Csag.Blueprint.TestHost.Endpoints.Auth.SwitchLanguage;

/// <summary>
/// The caller's new preferred language.
/// </summary>
public sealed class SwitchLanguageRequest
{
    /// <summary>
    /// Gets or sets the language code.
    /// </summary>
    public string Language { get; set; } = string.Empty;
}
