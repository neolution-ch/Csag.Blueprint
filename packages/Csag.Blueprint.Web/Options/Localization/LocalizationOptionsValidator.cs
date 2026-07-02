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

            this.RuleFor(x => x)
                .Must(x => x.SupportedLanguages.Contains(x.DefaultLanguage))
                .WithMessage("Default language must be included in the supported languages list");
        }
    }
}
