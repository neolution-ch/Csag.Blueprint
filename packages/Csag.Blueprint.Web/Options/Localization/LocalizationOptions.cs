namespace Csag.Blueprint.Web.Options.Localization
{
    /// <summary>
    /// Configuration options for the localization system.
    /// </summary>
    public sealed class LocalizationOptions
    {
        /// <summary>
        /// Gets or sets the default language code used as the primary fallback.
        /// </summary>
        public string DefaultLanguage { get; set; } = null!;

        /// <summary>
        /// Gets or sets the supported language codes.
        /// </summary>
        public IList<string> SupportedLanguages { get; set; } = null!;

        /// <summary>
        /// Gets or sets the L1 (in-memory) cache expiration time in minutes for translations.
        /// </summary>
        public int TranslationCacheL1ExpirationMinutes { get; set; }
    }
}
