namespace Csag.Blueprint.TestHost.Authorization;

/// <summary>
/// Authorization policy name constants. Endpoints reference these instead of string literals so
/// the policy registrations in <c>TestHostAuthorizationExtensions</c> and their usages cannot drift.
/// </summary>
internal static class PolicyNames
{
    /// <summary>
    /// Policy requiring the tenant-scoped <c>vehicles:read</c> permission.
    /// </summary>
    internal const string CanReadVehicles = "CanReadVehicles";

    /// <summary>
    /// Policy requiring the tenant-scoped <c>vehicles:manage</c> permission.
    /// </summary>
    internal const string CanManageVehicles = "CanManageVehicles";

    /// <summary>
    /// Policy requiring the tenant-scoped <c>members:manage</c> permission.
    /// </summary>
    internal const string CanManageMembers = "CanManageMembers";

    /// <summary>
    /// Policy requiring the platform-scope <c>tenants:manage</c> permission.
    /// </summary>
    internal const string CanManageTenants = "CanManageTenants";
}
