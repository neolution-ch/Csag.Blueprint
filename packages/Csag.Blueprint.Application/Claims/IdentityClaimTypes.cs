namespace Csag.Blueprint.Application.Claims;

/// <summary>
/// Defines identity claim type constants used within the application based on the blueprint template and packages.
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
}
