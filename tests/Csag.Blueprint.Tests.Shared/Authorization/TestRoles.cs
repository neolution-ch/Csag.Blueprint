namespace Csag.Blueprint.Tests.Shared.Authorization;
#pragma warning disable S2339 // Public constant members should not be used — string constants are deliberate so tests can use them in attributes and switch expressions.

/// <summary>
/// Role name constants for unit tests, split into tenant-scoped operational roles and
/// platform-scope roles (assigned globally, never per tenant).
/// </summary>
public static class TestRoles
{
    /// <summary>
    /// Tenant-scoped role with read-only permissions.
    /// </summary>
    public const string TenantViewer = "TenantViewer";

    /// <summary>
    /// Tenant-scoped role with manage permissions.
    /// </summary>
    public const string TenantManager = "TenantManager";

    /// <summary>
    /// Platform-scope role assigned globally (ASP.NET Identity user roles), never per tenant.
    /// Confers exactly the platform-scope permissions and no tenant operational permissions.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>
    /// Gets all defined role names.
    /// </summary>
    public static readonly string[] All = [TenantViewer, TenantManager, PlatformAdmin];

    /// <summary>
    /// Gets the platform-scope role names. Every role not listed here is a tenant-scoped operational role.
    /// </summary>
    public static readonly string[] PlatformScope = [PlatformAdmin];
}
#pragma warning restore S2339 // Public constant members should not be used
