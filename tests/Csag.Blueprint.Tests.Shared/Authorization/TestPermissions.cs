namespace Csag.Blueprint.Tests.Shared.Authorization;
#pragma warning disable S2339 // Public constant members should not be used — string constants are deliberate so tests can use them in attributes and switch expressions.

/// <summary>
/// Permission constants for unit tests, following the <c>resource:action</c> pattern and split
/// explicitly into tenant-scope and platform-scope permissions. Platform-scope permissions are
/// conferred only by platform-scope roles and must never be grantable directly within a tenant.
/// </summary>
public static class TestPermissions
{
    /// <summary>
    /// Tenant-scoped read permission for vehicles.
    /// </summary>
    public const string VehiclesRead = "vehicles:read";

    /// <summary>
    /// Tenant-scoped manage permission for vehicles.
    /// </summary>
    public const string VehiclesManage = "vehicles:manage";

    /// <summary>
    /// Tenant-scoped membership management permission.
    /// </summary>
    public const string MembersManage = "members:manage";

    /// <summary>
    /// Platform-scope cross-tenant management permission. Conferred only by the platform-scope
    /// <see cref="TestRoles.PlatformAdmin"/> role, never by tenant-scoped roles.
    /// </summary>
    public const string TenantsManage = "tenants:manage";

    /// <summary>
    /// Gets all defined permissions.
    /// </summary>
    public static readonly string[] All = [VehiclesRead, VehiclesManage, MembersManage, TenantsManage];

    /// <summary>
    /// Gets the tenant-scope permissions — the ones that may be granted directly within a tenant.
    /// </summary>
    public static readonly string[] TenantScope = [VehiclesRead, VehiclesManage, MembersManage];

    /// <summary>
    /// Gets the platform-scope permissions. Never grantable directly within a tenant.
    /// </summary>
    public static readonly string[] PlatformScope = [TenantsManage];
}
#pragma warning restore S2339 // Public constant members should not be used
