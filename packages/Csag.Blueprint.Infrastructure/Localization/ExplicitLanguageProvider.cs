namespace Csag.Blueprint.Infrastructure.Localization;

using Csag.Blueprint.Infrastructure.Abstractions.Services;

/// <summary>
/// Implementation of <see cref="ICurrentLanguageProvider"/> for callers that already know the
/// language, for example an endpoint that takes the language code as a request parameter.
/// <para>
/// Create one per call site instead of registering it in DI; use <see cref="DefaultLanguageProvider"/>
/// when the language should come from the ambient UI culture.
/// </para>
/// </summary>
public sealed class ExplicitLanguageProvider : ICurrentLanguageProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExplicitLanguageProvider"/> class.
    /// </summary>
    /// <param name="currentLanguageCode">The language to resolve content in.</param>
    /// <param name="fallbackLanguageCode">The language used when content is missing in the current language.</param>
    public ExplicitLanguageProvider(string currentLanguageCode, string fallbackLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentLanguageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackLanguageCode);

        this.CurrentLanguageCode = currentLanguageCode;
        this.FallbackLanguageCode = fallbackLanguageCode;
    }

    /// <inheritdoc/>
    public string CurrentLanguageCode { get; }

    /// <inheritdoc/>
    public string FallbackLanguageCode { get; }
}
