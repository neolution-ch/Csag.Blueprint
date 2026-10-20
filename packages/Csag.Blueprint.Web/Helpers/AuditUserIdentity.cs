namespace Csag.Blueprint.Web.Helpers;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;

/// <summary>
/// The identity of the user for an audit event. The values come from the claims on the request.
/// The package does not read them from the database.
/// </summary>
/// <remarks>
/// Both write paths use this type. The global <c>OnScopeCreated</c> enrichment uses it for an
/// Entity Framework event. <c>HttpAuditMiddleware</c> uses it for an audited HTTP request. So an
/// audited request and an Entity Framework entry for the same request always show the same user.
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

        // UserClaimsHelper.SetUserProfileClaims sets all three claims for a human user. A service-account
        // principal has no email address, and carries its client ID and account name under the raw
        // ServiceAccountClaimTypes names, because the package's bearer scheme does not map inbound claims.
        //
        // Read the mapped claim first. The values of a human user go directly into the mapped claims, and
        // they must have priority over a claim from an external provider.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(ServiceAccountClaimTypes.Subject);
        var email = user.FindFirstValue(ClaimTypes.Email);
        var displayName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ServiceAccountClaimTypes.Name);

        // Keep the full values. The package writes them only to the JSON data, which has no length
        // limit. If a value becomes a column, cut it to length here, one time. Do not cut it in the
        // column function of the provider: the JSON data and the column of one row are then different.
        return new AuditUserIdentity(userId, email, displayName);
    }
}
