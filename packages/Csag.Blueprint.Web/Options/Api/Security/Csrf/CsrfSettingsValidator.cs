namespace Csag.Blueprint.Web.Options.Api.Security.Csrf
{
    using FluentValidation;

    /// <summary>
    /// Validator for <see cref="CsrfSettings"/> configuration.
    /// Ensures all CSRF cookie and header names are properly configured.
    /// </summary>
    public sealed class CsrfSettingsValidator : AbstractValidator<CsrfSettings>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CsrfSettingsValidator"/> class.
        /// </summary>
        public CsrfSettingsValidator()
        {
            this.RuleFor(x => x.HeaderName)
                .NotEmpty()
                .WithMessage("CSRF header name must not be empty.");

            this.RuleFor(x => x.CookieName)
                .NotEmpty()
                .WithMessage("CSRF cookie name must not be empty.");

            this.RuleFor(x => x.RequestTokenCookieName)
                .NotEmpty()
                .WithMessage("CSRF request token cookie name must not be empty.");

            this.RuleFor(x => x.CookieName)
                .NotEqual(x => x.RequestTokenCookieName)
                .WithMessage("CSRF cookie name and request token cookie name must be different.");
        }
    }
}
