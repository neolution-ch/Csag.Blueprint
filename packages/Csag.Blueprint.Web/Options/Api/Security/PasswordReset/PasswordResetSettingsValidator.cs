namespace Csag.Blueprint.Web.Options.Api.Security.PasswordReset
{
    using FluentValidation;

    /// <summary>
    /// Validator for PasswordResetSettings configuration.
    /// Ensures password reset settings are valid and sensible.
    /// </summary>
    public sealed class PasswordResetSettingsValidator : AbstractValidator<PasswordResetSettings>
    {
        public PasswordResetSettingsValidator()
        {
            this.RuleFor(x => x.TokenLifetimeMinutes)
                .GreaterThanOrEqualTo(1)
                .WithMessage("TokenLifetimeMinutes must be at least 1")
                .LessThanOrEqualTo(1440)
                .WithMessage("TokenLifetimeMinutes must not exceed 1440 (24 hours)");

            this.RuleFor(x => x.FrontendBaseUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .When(x => !string.IsNullOrEmpty(x.FrontendBaseUrl))
                .WithMessage("FrontendBaseUrl must be a valid absolute URL");
        }
    }
}
