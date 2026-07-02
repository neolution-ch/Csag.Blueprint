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
}
