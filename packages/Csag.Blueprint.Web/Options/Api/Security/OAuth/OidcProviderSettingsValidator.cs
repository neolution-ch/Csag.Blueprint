namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using System;
    using FluentValidation;

    /// <summary>
    /// Validator for a single <see cref="OidcProviderSettings"/> entry.
    /// Only enabled providers are validated (see <see cref="OAuthSettingsValidator"/>); rules are
    /// profile-aware so that discovery can be driven either by an explicit authority
    /// (Generic/Google) or by the Entra sign-in audience.
    /// </summary>
    public sealed class OidcProviderSettingsValidator : AbstractValidator<OidcProviderSettings>
    {
        public OidcProviderSettingsValidator()
        {
            this.RuleFor(x => x.Profile)
                .IsInEnum()
                .WithMessage("OAuth provider Profile must be a valid value (Generic, Google, or Entra)");

            this.RuleFor(x => x.ClientId)
                .NotEmpty()
                .WithMessage("OAuth provider ClientId is required when the provider is enabled");

            this.RuleFor(x => x.ClientSecret)
                .NotEmpty()
                .WithMessage("OAuth provider ClientSecret is required when the provider is enabled")
                .MinimumLength(16)
                .WithMessage("OAuth provider ClientSecret must be at least 16 characters");

            this.RuleFor(x => x.Scopes)
                .NotEmpty()
                .WithMessage("OAuth provider Scopes is required when the provider is enabled")
                .Must(scopes =>
                {
                    if (string.IsNullOrEmpty(scopes))
                    {
                        return false;
                    }

                    var scopeList = scopes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return Array.IndexOf(scopeList, "openid") >= 0;
                })
                .WithMessage("OAuth provider Scopes must include 'openid'")
                .Must(scopes => string.IsNullOrEmpty(scopes) || !scopes.Contains(','))
                .WithMessage("OAuth provider Scopes must use semicolon (;) as delimiter, not comma");

            // Generic/Google discover via an explicit authority. Google may omit it (defaults to
            // https://accounts.google.com); when present it must be an absolute https URL.
            this.When(x => x.Profile != OidcProviderProfile.Entra, () =>
            {
                this.When(x => x.Profile == OidcProviderProfile.Generic, () =>
                {
                    this.RuleFor(x => x.Authority)
                        .NotEmpty()
                        .WithMessage("OAuth provider Authority is required for the Generic profile");
                });

                this.When(x => !string.IsNullOrWhiteSpace(x.Authority), () =>
                {
                    this.RuleFor(x => x.Authority)
                        .Must(BeAbsoluteHttpsUrl)
                        .WithMessage("OAuth provider Authority must be an absolute https URL");
                });
            });

            // Entra derives its authority from the sign-in audience; single-tenant needs a tenant GUID.
            this.When(x => x.Profile == OidcProviderProfile.Entra, () =>
            {
                this.RuleFor(x => x.SignInAudience)
                    .IsInEnum()
                    .WithMessage("OAuth provider SignInAudience must be a valid value (SingleTenant, MultiTenant, or MultiTenantAndPersonal)");

                this.When(x => x.SignInAudience == MicrosoftEntraSignInAudience.SingleTenant, () =>
                {
                    this.RuleFor(x => x.TenantId)
                        .NotEmpty()
                        .WithMessage("OAuth provider TenantId is required when the Entra SignInAudience is SingleTenant")
                        .Matches("^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$")
                        .WithMessage("OAuth provider TenantId must be a valid GUID");
                });
            });
        }

        private static bool BeAbsoluteHttpsUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps;
        }
    }
}
