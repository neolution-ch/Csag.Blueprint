namespace Csag.Blueprint.Infrastructure.Session;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Claims;

/// <summary>
/// Reads the values session tracking needs from a session ticket's principal, so the ticket store, the cookie events
/// and the session lifecycle agree on which claims carry them.
/// </summary>
internal static class SessionPrincipal
{
    /// <summary>
    /// Reads the account the principal authenticates.
    /// </summary>
    /// <param name="principal">The session principal.</param>
    /// <param name="accountId">The account identifier, when the principal carries one.</param>
    /// <returns><see langword="true"/> when the principal carries a parseable account identifier.</returns>
    public static bool TryGetAccountId(ClaimsPrincipal? principal, out Guid accountId)
        => Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out accountId);

    /// <summary>
    /// Reads the tenant the principal's session is scoped to.
    /// </summary>
    /// <param name="principal">The session principal.</param>
    /// <returns>The tenant identifier, or <see langword="null"/> for a session without a tenant.</returns>
    public static Guid? GetTenantId(ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(IdentityClaimTypes.TenantId), CultureInfo.InvariantCulture, out var tenantId)
            ? tenantId
            : null;
}
