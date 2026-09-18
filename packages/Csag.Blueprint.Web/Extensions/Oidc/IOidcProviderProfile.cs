namespace Csag.Blueprint.Web.Extensions.Oidc;

using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Applies a provider-specific configuration profile on top of the generic <c>AddOpenIdConnect</c> handler.
/// Implementations pre-wire safe defaults (authority, claim handling, issuer validation, email-trust policy)
/// for a given <see cref="OidcProviderProfile"/> so a single code path serves multiple identity providers.
/// </summary>
public interface IOidcProviderProfile
{
    /// <summary>
    /// Configures the OpenID Connect handler options for a provider. The scheme-dependent
    /// <c>OpenIdConnectOptions.CallbackPath</c> is set by the caller before this runs.
    /// </summary>
    /// <param name="options">The OpenID Connect options to configure.</param>
    /// <param name="settings">The validated provider settings.</param>
    void Configure(OpenIdConnectOptions options, OidcProviderSettings settings);

    /// <summary>
    /// Applies what must hold whatever the application configured on the scheme. Runs as a post-configure step,
    /// after every <c>Configure&lt;OpenIdConnectOptions&gt;</c>, so an application's own event handlers are
    /// composed with it rather than replacing it. Does nothing unless the profile needs it.
    /// </summary>
    /// <param name="scheme">The authentication scheme the options belong to.</param>
    /// <param name="options">The OpenID Connect options, after all configuration has run.</param>
    /// <param name="settings">The validated provider settings.</param>
    void PostConfigure(string scheme, OpenIdConnectOptions options, OidcProviderSettings settings)
    {
        // Most profiles are fully applied by Configure.
    }
}
