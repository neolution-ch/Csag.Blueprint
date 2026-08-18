namespace Csag.Blueprint.Web.Helpers;

using System.Security.Claims;

/// <summary>
/// The identity of the user for an audit event. The values come from the claims on the request.
/// The package does not read them from the database.
/// </summary>
/// <remarks>
/// Both write paths use this type: the global <c>OnScopeCreated</c> enrichment and
/// <c>HttpAuditMiddleware</c>. Therefore an HTTP entry and an Entity Framework entry for the same
/// request always show the same user.
/// </remarks>
/// <param name="UserId">The identifier of the user. Null if the request is anonymous.</param>
/// <param name="Email">The email address of the user. Null for a service account or an anonymous request.</param>
/// <param name="DisplayName">The display name of the user. Null if the principal has no name claim.</param>
internal readonly record struct AuditUserIdentity(string? UserId, string? Email, string? DisplayName)
{
    /// <summary>
    /// Reads the identity of the user from the principal of the request.
    /// </summary>
    /// <param name="user">The principal of the request. It can be null or unauthenticated.</param>
    /// <returns>The identity. All values are null if the principal has no identity claims.</returns>
    public static AuditUserIdentity FromPrincipal(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return default;
        }

        // UserClaimsHelper.SetUserProfileClaims sets all three claims for a human user. A service account
        // gets its claims from the token of the client-credentials endpoint, which has no email address.
        // That token holds the client ID in a raw "sub" claim and the account name in a raw "name" claim.
        // These two claims become ClaimTypes.NameIdentifier and ClaimTypes.Name only if JWT inbound claim
        // mapping is on. Therefore read the raw claims also.
        //
        // Read the mapped claim first. The values of a human user go directly into the mapped claims, and
        // they must have priority over a claim from an external provider. CurrentActorMiddleware reads
        // "sub" first, which is the opposite order.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        var email = user.FindFirstValue(ClaimTypes.Email);
        var displayName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");

        // Keep the full values. The package writes them only to the JSON data, which has no length
        // limit. If a value becomes a column, cut it to length here, one time. Do not cut it in the
        // column function of the provider: the JSON data and the column of one row are then different.
        return new AuditUserIdentity(userId, email, displayName);
    }
}
