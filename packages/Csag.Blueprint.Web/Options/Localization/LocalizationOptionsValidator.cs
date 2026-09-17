namespace Csag.Blueprint.Web.Options.Localization
{
    using FluentValidation;

    /// <summary>
    /// Validates <see cref="LocalizationOptions"/> configuration.
    /// </summary>
    public sealed class LocalizationOptionsValidator : AbstractValidator<LocalizationOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalizationOptionsValidator"/> class.
        /// </summary>
        public LocalizationOptionsValidator()
        {
            this.RuleFor(x => x.DefaultLanguage)
                .NotEmpty()
                .WithMessage("Default language must be specified");

            this.RuleFor(x => x.SupportedLanguages)
                .NotEmpty()
                .WithMessage("At least one supported language must be specified");

            // The containment check needs a list to search; a null SupportedLanguages is already
            // reported by the NotEmpty rule, so the check is skipped rather than dereferencing null.
            this.RuleFor(x => x)
                .Must(x => x.SupportedLanguages.Contains(x.DefaultLanguage))
                .WithMessage("Default language must be included in the supported languages list")
                .When(x => x.SupportedLanguages is not null);

            // The value feeds TimeSpan.FromMinutes for the L1 memory-cache entry lifetime; a
            // non-positive lifetime is rejected by the cache at insert time, so catch it at startup.
            this.RuleFor(x => x.TranslationCacheL1ExpirationMinutes)
                .GreaterThan(0)
                .WithMessage("TranslationCacheL1ExpirationMinutes must be greater than 0");
        }
    }
}
