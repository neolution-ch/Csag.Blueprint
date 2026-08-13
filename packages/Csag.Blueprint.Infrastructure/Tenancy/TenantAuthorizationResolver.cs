namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Default <see cref="ITenantAuthorizationResolver"/> implementation. Composes the tenant-scoped role and
/// permission services with the role→permission mapping to produce the effective authorization for a tenant.
/// </summary>
public sealed class TenantAuthorizationResolver : ITenantAuthorizationResolver
{
    private readonly ITenantRoleService tenantRoleService;
    private readonly ITenantPermissionService tenantPermissionService;
    private readonly IRolePermissionResolver rolePermissionResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAuthorizationResolver"/> class.
    /// </summary>
    /// <param name="tenantRoleService">Service used to load the user's tenant-scoped roles.</param>
    /// <param name="tenantPermissionService">Service used to load the user's direct tenant-scoped permission grants.</param>
    /// <param name="rolePermissionResolver">Resolver that maps role names to their derived permissions.</param>
    public TenantAuthorizationResolver(
        ITenantRoleService tenantRoleService,
        ITenantPermissionService tenantPermissionService,
        IRolePermissionResolver rolePermissionResolver)
    {
        this.tenantRoleService = tenantRoleService ?? throw new ArgumentNullException(nameof(tenantRoleService));
        this.tenantPermissionService = tenantPermissionService ?? throw new ArgumentNullException(nameof(tenantPermissionService));
        this.rolePermissionResolver = rolePermissionResolver ?? throw new ArgumentNullException(nameof(rolePermissionResolver));
    }

    /// <inheritdoc/>
    public Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveAsync(
        Guid? userId,
        IEnumerable<string> globalRoles,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(globalRoles);

        return this.ResolveInternalAsync(userId, globalRoles, tenantId, ct);
    }

    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveInternalAsync(
        Guid? userId,
        IEnumerable<string> globalRoles,
        Guid? tenantId,
        CancellationToken ct)
    {
        // Tenant-scoped lookups only apply when there is both a user and an active tenant.
        IReadOnlyList<string> tenantRoles = [];
        IReadOnlyList<string> directPermissions = [];

        if (tenantId.HasValue && userId.HasValue)
        {
            // Defense-in-depth: the write path (TenantRoleService/TenantPermissionService) rejects
            // platform-scope roles and non-tenant-grantable permissions, but rows that predate that check
            // (legacy data, manual SQL) must still never confer platform capabilities at authentication
            // time — so drop them here before unioning.
            tenantRoles = (await this.tenantRoleService.GetRoleNamesAsync(userId.Value, tenantId.Value, ct))
                .Where(role => !this.rolePermissionResolver.IsPlatformScopeRole(role))
                .ToList();
            directPermissions = (await this.tenantPermissionService.GetPermissionNamesAsync(userId.Value, tenantId.Value, ct))
                .Where(this.rolePermissionResolver.IsTenantGrantablePermission)
                .ToList();
        }

        // Only platform-scope roles are honored from the user's GLOBAL role set; operational roles are
        // strictly tenant-scoped. This prevents a global operational-role assignment (e.g. an OAuth default
        // role, or legacy AspNetUserRoles data) from leaking into every tenant.
        var platformGlobalRoles = globalRoles.Where(this.rolePermissionResolver.IsPlatformScopeRole);

        var roles = platformGlobalRoles.Concat(tenantRoles).Distinct(StringComparer.Ordinal).ToList();

        // The effective permission set is the union of role-derived permissions and the direct grants.
        var permissions = roles
            .SelectMany(this.rolePermissionResolver.GetPermissionsForRole)
            .Concat(directPermissions)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return (roles, permissions);
    }
}
