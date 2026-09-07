namespace Csag.Blueprint.Infrastructure.Enums;

/// <summary>
/// Strongly-typed cache identifiers for distributed caching.
/// Each enum value represents a distinct cache key category.
/// </summary>
public enum CacheId
{
    /// <summary>
    /// Authentication ticket cache identifier.
    /// Used for storing server-side authentication session data.
    /// </summary>
    AuthTicket = 0,

    /// <summary>
    /// Translation snapshot cache identifier.
    /// Used for storing merged translation dictionaries per language.
    /// </summary>
    Translation = 1,

    /// <summary>
    /// Service-account session cache identifier.
    /// Used as the fast-path reference-token backing store that marks a service-account session as
    /// live (keyed by its opaque session key); removed on revocation for immediate invalidation.
    /// </summary>
    ServiceAccountSession = 2,
}
