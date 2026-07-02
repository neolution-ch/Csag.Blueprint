namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using FluentValidation;

    /// <summary>
    /// Validator for OAuthSettings configuration.
    /// Ensures OAuth settings and all provider configurations are valid.
    /// </summary>
    public sealed class OAuthSettingsValidator : AbstractValidator<OAuthSettings>
    {
        public OAuthSettingsValidator()
        {
            this.RuleFor(x => x.Google)
                .NotNull()
                .WithMessage("OAuth.Google cannot be null");

            // Only validate Google settings when enabled
            this.When(x => x.Google?.Enabled == true, () =>
            {
                this.RuleFor(x => x.Google)
                    .SetValidator(new GoogleOAuthSettingsValidator());
            });
        }
    }
}
