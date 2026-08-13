namespace Csag.Blueprint.Web.Extensions.Oidc;

using System.Security.Claims;
using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Profile for any standards-compliant OpenID Connect provider (Auth0, Okta, Keycloak, ...).
/// Discovery is driven by the configured <see cref="OidcProviderSettings.Authority"/>; the default
/// inbound claim map produces the standard <see cref="ClaimTypes"/> the callback reads, and the token's
/// <c>email_verified</c> claim flows through for the callback's email-trust gate.
/// </summary>
public sealed class GenericOidcProfile : OidcProviderProfileBase
{
    /// <inheritdoc/>
    public override void Configure(OpenIdConnectOptions options, OidcProviderSettings settings)
    {
        ApplyCommon(options, settings);

        if (!string.IsNullOrWhiteSpace(settings.Authority))
        {
            options.Authority = settings.Authority;
        }

        ApplyIssuerValidation(options, settings);

        // email_verified is not part of the default inbound claim map, so it reaches the callback
        // literally from the id_token; also surface it from userinfo when a provider only sends it there.
        options.ClaimActions.MapJsonKey("email_verified", "email_verified");
    }
}
