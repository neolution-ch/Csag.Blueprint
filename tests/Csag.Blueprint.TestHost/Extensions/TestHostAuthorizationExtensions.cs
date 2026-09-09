namespace Csag.Blueprint.TestHost.Extensions;

using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.TestHost.Authorization;
using Csag.Blueprint.Tests.Shared.Authorization;

/// <summary>
/// Registers permission-based authorization policies over the shared test permissions.
/// </summary>
public static class TestHostAuthorizationExtensions
{
    /// <summary>
    /// Adds one claim-requirement policy per test permission. Permission claims are placed on the
    /// principal at sign-in (and re-derived from role claims by the claims transformation), so the
    /// policies never hit the database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestHostAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.CanReadVehicles, policy =>
                policy.RequireClaim(IdentityClaimTypes.Permission, TestPermissions.VehiclesRead));

            options.AddPolicy(PolicyNames.CanManageVehicles, policy =>
                policy.RequireClaim(IdentityClaimTypes.Permission, TestPermissions.VehiclesManage));

            options.AddPolicy(PolicyNames.CanManageMembers, policy =>
                policy.RequireClaim(IdentityClaimTypes.Permission, TestPermissions.MembersManage));

            // Platform-scope capability, conferred only by the global PlatformAdmin role.
            options.AddPolicy(PolicyNames.CanManageTenants, policy =>
                policy.RequireClaim(IdentityClaimTypes.Permission, TestPermissions.TenantsManage));
        });

        return services;
    }
}
