namespace Csag.Blueprint.Web.Extensions.Oidc;

using Csag.Blueprint.Web.Options.Api.Security.OAuth;

/// <summary>
/// Resolves the <see cref="IOidcProviderProfile"/> implementation for a configured
/// <see cref="OidcProviderProfile"/>. Profiles are stateless.
/// </summary>
public static class OidcProviderProfileFactory
{
    /// <summary>
    /// Gets the profile implementation for the given profile kind.
    /// </summary>
    public static IOidcProviderProfile For(OidcProviderProfile profile)
    {
        return profile switch
        {
            OidcProviderProfile.Google => new GoogleOidcProfile(),
            OidcProviderProfile.Entra => new EntraOidcProfile(),
            _ => new GenericOidcProfile(),
        };
    }
}
