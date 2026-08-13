namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Resolves the effective authorization — the merged role set and the full permission set — for a user
/// within an optional tenant. This is the single source of truth used by every code path that bakes
/// authorization claims into an authentication ticket (sign-in, session refresh, and tenant switch), so
/// the rule for combining platform roles, tenant-scoped roles, role-derived permissions, and direct
/// tenant-scoped permission grants lives in exactly one place.
/// </summary>
public interface ITenantAuthorizationResolver
{
    /// <summary>
    /// Resolves the effective roles and permissions for a user in the context of a tenant.
    /// </summary>
    /// <param name="userId">The user identifier, or <see langword="null"/> if it could not be determined,
    /// in which case no tenant-scoped lookups are performed.</param>
    /// <param name="globalRoles">The user's platform-scope (global ASP.NET Identity) role names.</param>
    /// <param name="tenantId">The active tenant, or <see langword="null"/> when there is no tenant context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The merged platform- and tenant-scope role names, and the full effective permission set
    /// (role-derived permissions unioned with the user's direct tenant-scoped grants).
    /// </returns>
    Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveAsync(
        Guid? userId,
        IEnumerable<string> globalRoles,
        Guid? tenantId,
        CancellationToken ct = default);
}
