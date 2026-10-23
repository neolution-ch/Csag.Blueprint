namespace Csag.Blueprint.Infrastructure.Session;

/// <summary>
/// Constants related to session management.
/// </summary>
public static class SessionConstants
{
    /// <summary>
    /// The key used to store the session key in authentication ticket properties.
    /// </summary>
    public static readonly string SessionKeyPropertyName = ".SessionKey";

    /// <summary>
    /// The longest session key, in percent-encoded UTF-8 bytes, a user session can be tracked under.
    /// </summary>
    /// <remarks>
    /// The distributed cache composes the ticket's key as "CacheId:AuthTicket_" + Uri.EscapeDataString(key) and
    /// rejects a generated key over 250 UTF-8 bytes, so the 19-byte prefix leaves 231 for the encoded key.
    /// Percent-encoding never shrinks a string, so a key inside this budget also fits the 500-character SessionKey
    /// column: the cache is the binding limit of the two. The prefix assumes the cache's default key options — an
    /// application that configures an environment prefix or a schema version lengthens the generated key and lowers
    /// its own effective ceiling below this one. That residual gap cannot strand a row, because
    /// <see cref="DistributedCacheTicketStore"/> writes the ticket under the key before it tracks the session: a key
    /// the configured cache refuses fails the sign-in before any row exists.
    /// </remarks>
    internal const int SessionKeyMaxCacheKeyBytes = 231;
}
