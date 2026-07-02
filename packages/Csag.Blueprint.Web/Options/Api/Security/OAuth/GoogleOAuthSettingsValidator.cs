namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using FluentValidation;

    /// <summary>
    /// Validator for GoogleOAuthSettings configuration.
    /// Ensures Google OAuth settings are valid when enabled.
    /// </summary>
    public sealed class GoogleOAuthSettingsValidator : AbstractValidator<GoogleOAuthSettings>
    {
        public GoogleOAuthSettingsValidator()
        {
            // Only validate when Google OAuth is enabled
            this.When(x => x.Enabled, () =>
            {
                this.RuleFor(x => x.ClientId)
                    .NotEmpty()
                    .WithMessage("OAuth.Google.ClientId is required when Google OAuth is enabled");

                this.RuleFor(x => x.ClientSecret)
                    .NotEmpty()
                    .WithMessage("OAuth.Google.ClientSecret is required when Google OAuth is enabled")
                    .MinimumLength(16)
                    .WithMessage("OAuth.Google.ClientSecret must be at least 16 characters");

                this.RuleFor(x => x.Scopes)
                    .NotEmpty()
                    .WithMessage("OAuth.Google.Scopes is required when Google OAuth is enabled")
                    .Must(scopes =>
                    {
                        var scopeList = scopes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        return scopeList.Contains("openid") && scopeList.Contains("profile") && scopeList.Contains("email");
                    })
                    .WithMessage("OAuth.Google.Scopes must include at minimum: openid;profile;email");
            });

            // Always validate Scopes format (even when disabled, in case it's set)
            this.RuleFor(x => x.Scopes)
                .Must(scopes => string.IsNullOrEmpty(scopes) || !scopes.Contains(','))
                .WithMessage("OAuth.Google.Scopes must use semicolon (;) as delimiter, not comma");
        }
    }
}
