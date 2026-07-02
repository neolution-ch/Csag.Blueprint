namespace Csag.Blueprint.Web.Options.Api.Security
{
    using Csag.Blueprint.Web.Options.Api.Security.Cors;
    using Csag.Blueprint.Web.Options.Api.Security.Csrf;
    using Csag.Blueprint.Web.Options.Api.Security.HttpsRedirect;
    using Csag.Blueprint.Web.Options.Api.Security.Jwt;
    using Csag.Blueprint.Web.Options.Api.Security.OAuth;
    using Csag.Blueprint.Web.Options.Api.Security.Password;
    using Csag.Blueprint.Web.Options.Api.Security.PasswordReset;
    using Csag.Blueprint.Web.Options.Api.Security.SecurityHeaders;
    using FluentValidation;

    /// <summary>
    /// Validator for SecuritySettings configuration.
    /// Ensures security settings including CORS, HTTPS redirect, security headers, password policy, and OAuth are valid.
    /// </summary>
    public sealed class SecuritySettingsValidator : AbstractValidator<SecuritySettings>
    {
        public SecuritySettingsValidator()
        {
            this.RuleFor(x => x.CorsPolicies)
                .NotNull()
                .WithMessage("CorsPolicies cannot be null");

            this.RuleFor(x => x.HttpsRedirect)
                .NotNull()
                .WithMessage("HttpsRedirect cannot be null")
                .SetValidator(new HttpsRedirectSettingsValidator());

            this.RuleFor(x => x.SecurityHeaders)
                .NotNull()
                .WithMessage("SecurityHeaders cannot be null")
                .SetValidator(new SecurityHeadersSettingsValidator());

            this.RuleFor(x => x.PasswordSettings)
                .NotNull()
                .WithMessage("PasswordSettings cannot be null")
                .SetValidator(new PasswordSettingsValidator());

            this.RuleFor(x => x.OAuth)
                .NotNull()
                .WithMessage("OAuth cannot be null")
                .SetValidator(new OAuthSettingsValidator());

            this.RuleFor(x => x.PasswordResetSettings)
                .NotNull()
                .WithMessage("PasswordResetSettings cannot be null")
                .SetValidator(new PasswordResetSettingsValidator());

            this.RuleFor(x => x.Jwt)
                .NotNull()
                .WithMessage("Jwt cannot be null")
                .SetValidator(new JwtSettingsValidator());

            this.RuleFor(x => x.Csrf)
                .NotNull()
                .WithMessage("Csrf cannot be null")
                .SetValidator(new CsrfSettingsValidator());

            this.RuleFor(x => x.SessionExpirationHours)
                .GreaterThan(0)
                .WithMessage("SessionExpirationHours must be greater than 0");

            this.RuleFor(x => x.CookieSecurePolicy)
                .IsInEnum()
                .WithMessage("CookieSecurePolicy must be a valid value (Always, SameAsRequest, or None).");

            this.RuleFor(x => x.CorsPolicies)
                .NotEmpty()
                .WithMessage("At least one CORS policy must be defined in Blueprint:Security:CorsPolicies");

            this.When(x => !string.IsNullOrEmpty(x.DefaultCorsPolicy), () =>
            {
                this.RuleFor(x => x.DefaultCorsPolicy)
                    .Must((settings, defaultPolicy) => settings.CorsPolicies.ContainsKey(defaultPolicy!))
                    .WithMessage(x => $"DefaultCorsPolicy '{x.DefaultCorsPolicy}' must exist in CorsPolicies dictionary");
            });

            // Validate each CORS policy in the dictionary
            this.RuleForEach(x => x.CorsPolicies)
                .ChildRules(policy =>
                {
                    policy.RuleFor(x => x.Key)
                        .NotEmpty()
                        .WithMessage("CORS policy name cannot be empty")
                        .Matches("^[a-zA-Z0-9_-]+$")
                        .WithMessage("CORS policy name must contain only letters, numbers, hyphens, and underscores");

                    policy.RuleFor(x => x.Value)
                        .SetValidator(new CorsSettingsValidator());
                });
        }
    }
}
