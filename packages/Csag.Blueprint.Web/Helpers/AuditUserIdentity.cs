namespace Csag.Blueprint.Web.Helpers;

using System.Security.Claims;

/// <summary>
/// The acting user's identity as captured for an audit event: read from the claims already on the
/// request, never from a database lookup.
/// </summary>
/// <remarks>
/// Both write paths — the global <c>OnScopeCreated</c> enrichment and <c>HttpAuditMiddleware</c> —
/// resolve the acting user through this type, so an HTTP entry and an EF entry written for the same
/// request cannot disagree about who caused them.
/// </remarks>
/// <param name="UserId">The user identifier, or null when the request is anonymous.</param>
/// <param name="Email">The user's email address, or null for service accounts and anonymous requests.</param>
/// <param name="DisplayName">The user's display name, or null when the principal carries no name claim.</param>
internal readonly record struct AuditUserIdentity(string? UserId, string? Email, string? DisplayName)
{
    /// <summary>
    /// Resolves the acting user from the request principal.
    /// </summary>
    /// <param name="user">The principal on the current request, which may be null or unauthenticated.</param>
    /// <returns>The captured identity; every member is null when there is no authenticated user.</returns>
    public static AuditUserIdentity FromPrincipal(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return default;
        }

        // Human logins get all three claims from UserClaimsHelper.SetUserProfileClaims, the single funnel
        // for login, external sign-in and session refresh. Service accounts get theirs from the token
        // issued by the client-credentials endpoint, which carries no email at all.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);

        // A service-account token carries its account name as a raw "name" claim, which only surfaces as
        // ClaimTypes.Name while JWT inbound claim mapping is enabled. Fall back to the raw claim so the
        // capture does not depend on that setting, the way CurrentActorMiddleware falls back from "sub" to
        // ClaimTypes.NameIdentifier. Mapped first, because a human's display name is written straight to
        // ClaimTypes.Name and must win over any "name" an external provider left on the identity.
        var displayName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");

        // Captured whole. These values are only ever written into the audit event's JSON, which is an
        // unbounded column, so nothing here needs clipping. Should either value ever be promoted to a
        // real column, the clip belongs here — at capture, once — and not in the provider's column
        // lambda, which would leave the JSON and the column disagreeing on the same row.
        return new AuditUserIdentity(userId, email, displayName);
    }
}
