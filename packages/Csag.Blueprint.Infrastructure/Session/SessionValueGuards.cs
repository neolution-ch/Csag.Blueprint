namespace Csag.Blueprint.Infrastructure.Session;

using System.Diagnostics.CodeAnalysis;
using System.Text;

/// <summary>
/// The value rules <see cref="SessionManager{TUser, TContext}"/> and
/// <see cref="ServiceAccountSessionManager{TContext}"/> both apply. Each manager keys its own slot of the same
/// <c>IDistributedCache</c> by session key and stores a client-supplied User-Agent and IP alongside its
/// tracking row, so the two non-obvious rules — measure a key in percent-encoded UTF-8 bytes
/// rather than in characters, and never cut a clamped value between surrogates — are stated once here. The byte
/// budget itself stays with each manager, because it is what remains of the cache's key limit after that
/// manager's own cache-id prefix; the column lengths stay there too, because each mirrors that manager's own
/// entity configuration.
/// </summary>
internal static class SessionValueGuards
{
    /// <summary>
    /// Measures a session key the way the distributed cache measures it. The cache percent-encodes the key
    /// before checking the generated key's length, so every character outside the URI unreserved set costs three
    /// bytes (twelve for a non-BMP character) and a standard-base64 key of <paramref name="maxCacheKeyBytes"/>
    /// characters is already over budget.
    /// </summary>
    /// <param name="sessionKey">The session key to measure.</param>
    /// <param name="maxCacheKeyBytes">What the caller's cache-id prefix leaves of the cache's key limit, in UTF-8 bytes.</param>
    /// <returns><c>true</c> when the percent-encoded key exceeds the budget.</returns>
    public static bool ExceedsCacheKeyBudget(string sessionKey, int maxCacheKeyBytes)
        => Encoding.UTF8.GetByteCount(Uri.EscapeDataString(sessionKey)) > maxCacheKeyBytes;

    /// <summary>
    /// Reports whether a session key is one a tracked session could carry: neither blank nor over budget, the
    /// bounds the tracking path enforces before it stores a key. By-key lookups filter on this instead of
    /// handing a client-supplied key to the cache, which throws on an over-long key and silently redirects a
    /// blank one to the shared slot of the cache id it was given.
    /// </summary>
    /// <param name="sessionKey">The session key to check; <c>null</c> is reported the same way a blank key is.</param>
    /// <param name="maxCacheKeyBytes">What the caller's cache-id prefix leaves of the cache's key limit, in UTF-8 bytes.</param>
    /// <returns><c>true</c> when the key is neither blank nor over budget.</returns>
    public static bool IsWellFormedSessionKey([NotNullWhen(true)] string? sessionKey, int maxCacheKeyBytes)
        => !string.IsNullOrWhiteSpace(sessionKey) && !ExceedsCacheKeyBudget(sessionKey, maxCacheKeyBytes);

    /// <summary>
    /// Clamps a diagnostic value to its mapped column length, so a client-supplied over-length value cannot turn
    /// session tracking into a SQL truncation error.
    /// </summary>
    /// <param name="value">The value to clamp; <c>null</c> and already-short values pass through unchanged.</param>
    /// <param name="maxLength">The mapped column length, in UTF-16 code units.</param>
    /// <returns>The value, shortened to at most <paramref name="maxLength"/> characters.</returns>
    public static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        // Never cut between a surrogate pair. A lone surrogate survives the nvarchar round-trip but is not
        // valid UTF-16, so it fails or is replaced wherever the value is serialized back out.
        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }
}
