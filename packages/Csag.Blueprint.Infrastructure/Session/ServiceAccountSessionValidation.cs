namespace Csag.Blueprint.Infrastructure.Session;

/// <summary>
/// The server-side state resolved for a valid service-account session. All values reflect the account's current
/// database state at request time, not what was baked into the token at issuance.
/// </summary>
internal sealed class ServiceAccountSessionValidation
{
    /// <summary>
    /// Gets the service account the session belongs to.
    /// </summary>
    public required Guid ServiceAccountId { get; init; }

    /// <summary>
    /// Gets the account's client id.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the account's current display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the tenant the service account is scoped to.
    /// </summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Gets the account's current role assignments.
    /// </summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>
    /// Gets the account's current explicit permission grants (role-derived permissions are added separately
    /// by the permission claims transformation).
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}
