namespace Csag.Blueprint.Infrastructure.Authentication;

/// <summary>
/// The authentication scheme names the package registers. Each request is authenticated by exactly one scheme: the
/// two selector schemes forward every request to a single handler.
/// </summary>
public static class BlueprintAuthenticationSchemes
{
    /// <summary>
    /// Gets the name of the bearer token selector. It forwards a request to the handler of the token kind its bearer
    /// token belongs to (see <see cref="BlueprintBearerOptions"/>), and to <see cref="ServiceAccountToken"/> when no
    /// other kind claims it. Endpoints that accept bearer tokens only list this scheme.
    /// </summary>
    public static readonly string Bearer = "Bearer";

    /// <summary>
    /// Gets the name of the scheme that validates service-account tokens. Registered behind <see cref="Bearer"/>;
    /// endpoints list the selector, not this scheme.
    /// </summary>
    public static readonly string ServiceAccountToken = "Blueprint.ServiceAccountToken";

    /// <summary>
    /// Gets the name of the per-request selector between the session cookie and bearer tokens. It forwards a request
    /// that carries a bearer token (see <see cref="BearerTokenRequestExtensions.TryGetBearerToken"/>) to
    /// <see cref="Bearer"/>, and every other request to the Identity application cookie. Endpoints that accept both
    /// list this scheme alone.
    /// </summary>
    public static readonly string SessionOrBearer = "Blueprint.SessionOrBearer";
}
