namespace Csag.Blueprint.Application.Abstractions.Services;

using System.Security.Claims;
using Csag.Blueprint.Application.Claims;

/// <summary>
/// What an <see cref="IServiceAccountClaimsEnricher"/> is given: the account a request authenticated as, and the
/// identity the request's principal is built from.
/// </summary>
public sealed class ServiceAccountClaimsContext
{
    /// <summary>
    /// Gets the account the request authenticated as.
    /// </summary>
    public required Guid ServiceAccountId { get; init; }

    /// <summary>
    /// Gets the account's tenant.
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Gets the identity being built. It already carries the <see cref="ServiceAccountClaimTypes"/> claims, the tenant
    /// claim, and the account's current role and permission claims.
    /// </summary>
    public required ClaimsIdentity Identity { get; init; }
}
