namespace Csag.Blueprint.Web.Extensions.Oidc;

using Csag.Blueprint.Web.Options.Api.Security.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
/// Profile for Google. Behaves like <see cref="GenericOidcProfile"/> but defaults the authority to
/// Google's issuer, so only client credentials need to be configured. Google's id_token carries a
/// standard <c>email_verified</c> claim that the callback's email-trust gate consumes directly.
/// </summary>
public sealed class GoogleOidcProfile : OidcProviderProfileBase
{
    /// <summary>Google's OpenID Connect issuer / discovery authority.</summary>
    private const string DefaultAuthority = "https://accounts.google.com";

    /// <inheritdoc/>
    public override void Configure(OpenIdConnectOptions options, OidcProviderSettings settings)
    {
        ApplyCommon(options, settings);

        options.Authority = string.IsNullOrWhiteSpace(settings.Authority) ? DefaultAuthority : settings.Authority;

        ApplyIssuerValidation(options, settings);

        options.ClaimActions.MapJsonKey("email_verified", "email_verified");
    }
}
