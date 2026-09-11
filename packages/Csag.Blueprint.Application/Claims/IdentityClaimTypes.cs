namespace Csag.Blueprint.Application.Claims;

/// <summary>
/// Defines identity claim type constants shared between the Blueprint packages and consuming applications.
/// </summary>
public static class IdentityClaimTypes
{
    /// <summary>
    /// Gets the claim type for the tenant identifier.
    /// Stored in the authentication ticket so it's available on every request without a DB lookup.
    /// </summary>
    public static readonly string TenantId = "TenantId";

    /// <summary>
    /// Gets the claim type for permission claims.
    /// Used to store authorization permissions in the authentication ticket.
    /// </summary>
    public static readonly string Permission = "Permission";

    /// <summary>
    /// Gets the claim type for the user's preferred language code (e.g., "de-CH", "en-GB").
    /// Stored in the authentication ticket so it's available on every request without a DB lookup.
    /// </summary>
    public static readonly string PreferredLanguage = "PreferredLanguage";

    /// <summary>
    /// Gets the claim type carrying the opaque service-account session id ("sid"). It is the only
    /// authorization-relevant claim a reference-style service-account token carries; roles, permissions,
    /// and tenant are resolved server-side from this session on every request rather than trusted from
    /// the token.
    /// </summary>
    public static readonly string ServiceAccountSessionId = "sid";
}
