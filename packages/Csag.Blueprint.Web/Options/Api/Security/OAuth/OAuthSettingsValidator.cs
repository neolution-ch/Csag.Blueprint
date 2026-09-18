namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FluentValidation;

    /// <summary>
    /// Validator for OAuthSettings configuration.
    /// Ensures OAuth settings and all configured OIDC provider entries are valid.
    /// </summary>
    public sealed class OAuthSettingsValidator : AbstractValidator<OAuthSettings>
    {
        // Provider keys become the {provider} route segment under /auth/external. These segments are already
        // occupied by sibling literal routes (/auth/external/callback, /auth/external/providers), which win
        // route precedence, so a provider using one would register a scheme whose challenge route is shadowed
        // and unreachable. Reject such keys up front instead of failing silently at runtime.
        private static readonly string[] ReservedSchemeKeys = ["callback", "providers"];

        public OAuthSettingsValidator()
        {
            // When set, the frontend base URL is used as a trusted origin for post-login redirects,
            // so it must be a well-formed absolute http(s) URL.
            this.When(x => !string.IsNullOrWhiteSpace(x.FrontendBaseUrl), () =>
            {
                this.RuleFor(x => x.FrontendBaseUrl)
                    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
                    .WithMessage("OAuth.FrontendBaseUrl must be an absolute http(s) URL (e.g. \"https://app.example.com\")");
            });

            this.RuleFor(x => x.Providers)
                .NotNull()
                .WithMessage("OAuth.Providers cannot be null");

            // Validate each enabled provider with the shared, profile-aware provider validator. Guarded on a
            // non-null dictionary so a "Providers": null override surfaces the NotNull message above rather
            // than throwing an NRE while enumerating x.Providers.Values.
            this.When(x => x.Providers != null, () =>
            {
                this.RuleForEach(x => x.Providers.Values)
                    .Where(provider => provider.Enabled)
                    .SetValidator(new OidcProviderSettingsValidator());
            });

            // Cross-provider uniqueness: the per-provider validator above cannot see siblings, but the
            // effective CallbackPath and DisplayName must be distinct across enabled providers. Two schemes
            // sharing a callback path make the wrong OpenID Connect handler intercept the redirect
            // (correlation failure), and a shared DisplayName collides the persisted
            // AspNetUserLogins.LoginProvider. Both boot successfully otherwise but break login silently.
            // Reserved keys collide with the sibling literal routes under /auth/external and would leave the
            // provider's challenge route unreachable.
            this.When(x => x.Providers != null, () =>
            {
                this.RuleFor(x => x.Providers)
                    .Must(HaveUniqueCallbackPaths)
                    .WithMessage("OAuth provider CallbackPath values must be unique across enabled providers")
                    .Must(HaveUniqueDisplayNames)
                    .WithMessage("OAuth provider DisplayName values must be unique across enabled providers")
                    .Must(HaveNoReservedSchemeKeys)
                    .WithMessage("OAuth provider keys may not be a reserved route segment (callback, providers)");
            });

            // With a frontend base URL, providers return the user to the frontend origin, which only forwards
            // the proxied prefix to the API. A callback path outside it would land on the SPA instead.
            this.When(x => !string.IsNullOrWhiteSpace(x.FrontendBaseUrl) && x.Providers != null, () =>
            {
                this.RuleFor(x => x.Providers)
                    .Must(providers => !UnproxiedProviders(providers).Any())
                    .WithMessage(x =>
                        $"OAuth provider(s) {string.Join(", ", UnproxiedProviders(x.Providers))} need a CallbackPath under " +
                        $"{OidcCallbackPaths.ProxiedPrefix} with no empty, '.' or '..' segments, because OAuth.FrontendBaseUrl " +
                        "is set and that origin only forwards those paths to the API");
            });
        }

        private static bool HaveUniqueCallbackPaths(IDictionary<string, OidcProviderSettings> providers)
        {
            var paths = providers
                .Where(kvp => kvp.Value.Enabled)
                .Select(kvp => OidcCallbackPaths.Resolve(kvp.Key, kvp.Value))
                .ToList();

            return paths.Count == paths.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        private static IEnumerable<string> UnproxiedProviders(IDictionary<string, OidcProviderSettings> providers)
        {
            return providers
                .Where(kvp => kvp.Value.Enabled
                    && !OidcCallbackPaths.IsUnderProxiedPrefix(OidcCallbackPaths.Resolve(kvp.Key, kvp.Value)))
                .Select(kvp => kvp.Key);
        }

        private static bool HaveUniqueDisplayNames(IDictionary<string, OidcProviderSettings> providers)
        {
            // DisplayName defaults to the scheme key (which is already unique), so only explicit collisions fail.
            var names = providers
                .Where(kvp => kvp.Value.Enabled)
                .Select(kvp => string.IsNullOrWhiteSpace(kvp.Value.DisplayName) ? kvp.Key : kvp.Value.DisplayName!)
                .ToList();

            return names.Count == names.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        private static bool HaveNoReservedSchemeKeys(IDictionary<string, OidcProviderSettings> providers)
        {
            return !providers
                .Where(kvp => kvp.Value.Enabled)
                .Any(kvp => ReservedSchemeKeys.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase));
        }
    }
}
