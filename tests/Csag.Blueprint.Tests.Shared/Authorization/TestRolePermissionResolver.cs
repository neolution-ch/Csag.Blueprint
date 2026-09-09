namespace Csag.Blueprint.Tests.Shared.Authorization;

using Csag.Blueprint.Application.Abstractions.Authorization;

/// <summary>
/// Test implementation of <see cref="IRolePermissionResolver"/> mapping the <see cref="TestRoles"/>
/// to their <see cref="TestPermissions"/>: a read-only tenant role, a managing tenant role, and a
/// platform-scope role that confers exactly the platform-scope permissions.
/// </summary>
public sealed class TestRolePermissionResolver : IRolePermissionResolver
{
    private static readonly string[] TenantViewerPermissions = [TestPermissions.VehiclesRead];

    private static readonly string[] TenantManagerPermissions =
    [
        TestPermissions.VehiclesRead,
        TestPermissions.VehiclesManage,
        TestPermissions.MembersManage,
    ];

    /// <inheritdoc/>
    public IEnumerable<string> GetPermissionsForRole(string role)
    {
        return role switch
        {
            TestRoles.TenantViewer => TenantViewerPermissions,
            TestRoles.TenantManager => TenantManagerPermissions,
            TestRoles.PlatformAdmin => TestPermissions.PlatformScope,
            _ => [],
        };
    }

    /// <inheritdoc/>
    public bool IsPlatformScopeRole(string role)
    {
        return TestRoles.PlatformScope.Contains(role, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public bool IsTenantGrantablePermission(string permission)
    {
        return TestPermissions.TenantScope.Contains(permission, StringComparer.Ordinal);
    }
}
