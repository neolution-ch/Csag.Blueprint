namespace Csag.Blueprint.Infrastructure.Localization;

using System.Globalization;
using Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Default implementation of <see cref="ICurrentLanguageProvider"/> that resolves the current
/// language from <see cref="CultureInfo.CurrentUICulture"/>.
/// <para>
/// In an ASP.NET Core application the request localization middleware sets the UI culture per
/// request, so registering this as a scoped service is usually enough. Applications that resolve
/// the language from somewhere else (user profile, tenant setting, explicit request parameter)
/// should provide their own implementation.
/// </para>
/// </summary>
public sealed class DefaultLanguageProvider : ICurrentLanguageProvider
{
    private readonly string fallbackLanguageCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultLanguageProvider"/> class.
    /// </summary>
    /// <param name="fallbackLanguageCode">
    /// The language code used when content is missing in the current language, and when the ambient
    /// culture is the invariant culture. Defaults to "en".
    /// </param>
    public DefaultLanguageProvider(string fallbackLanguageCode = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackLanguageCode);
        this.fallbackLanguageCode = fallbackLanguageCode;
    }

    /// <inheritdoc/>
    public string CurrentLanguageCode
    {
        get
        {
            // The invariant culture has an empty name and carries no language information.
            var currentName = CultureInfo.CurrentUICulture.Name;
            return string.IsNullOrEmpty(currentName) ? this.fallbackLanguageCode : currentName;
        }
    }

    /// <inheritdoc/>
    public string FallbackLanguageCode => this.fallbackLanguageCode;
}
