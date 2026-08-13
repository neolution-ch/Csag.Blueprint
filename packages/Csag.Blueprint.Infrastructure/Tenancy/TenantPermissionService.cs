namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantPermissionService"/>. Reads and writes
/// <see cref="BlueprintTenantMembershipPermission{TUser,TTenant}"/> rows. Permissions are stored as
/// their literal string values, so — unlike <see cref="TenantRoleService{TUser,TTenant,TContext}"/> —
/// no catalog lookup is required.
/// </summary>
/// <typeparam name="TUser">The concrete user type.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type.</typeparam>
/// <typeparam name="TContext">The application's <see cref="DbContext"/> type.</typeparam>
#pragma warning disable S2436 // Three generic parameters mirror the persistence-backed manager pattern used across the Blueprint packages.
public sealed class TenantPermissionService<TUser, TTenant, TContext> : ITenantPermissionService
#pragma warning restore S2436
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
    where TContext : DbContext
{
    private readonly TContext dbContext;
    private readonly IRolePermissionResolver rolePermissionResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantPermissionService{TUser, TTenant, TContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    /// <param name="rolePermissionResolver">Resolver used to reject permissions that are not tenant-grantable.</param>
    public TenantPermissionService(TContext dbContext, IRolePermissionResolver rolePermissionResolver)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.rolePermissionResolver = rolePermissionResolver ?? throw new ArgumentNullException(nameof(rolePermissionResolver));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        return await this.dbContext.Set<BlueprintTenantMembershipPermission<TUser, TTenant>>()
            .AsNoTracking()
            .Where(membershipPermission => membershipPermission.UserId == userId && membershipPermission.TenantId == tenantId)
            .Select(membershipPermission => membershipPermission.Permission)
            .OrderBy(permission => permission)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public Task SetPermissionsAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return SetPermissionsInternalAsync();

        async Task SetPermissionsInternalAsync()
        {
            // Permission rows hang off the tenant membership (their FK cascades from it). Verify the
            // membership inside the write path so a violated precondition — a caller that skipped the
            // membership, or a concurrent membership removal — surfaces as a clear error instead of a raw
            // FK violation from SaveChanges. Mirrors the check in TenantRoleService.
            var isMember = await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
                .AnyAsync(membership => membership.UserId == userId && membership.TenantId == tenantId, ct);
            if (!isMember)
            {
                throw new TenantMembershipRequiredException($"User '{userId}' is not a member of tenant '{tenantId}'; tenant-scoped permission grants require an existing membership.");
            }

            var desiredPermissions = permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            // Only tenant-grantable permissions may be persisted as direct grants: platform-scope
            // permissions (e.g. tenants:manage) and unknown values would confer capabilities at
            // authentication time. This enforces the invariant at the persistence seam, independent of
            // any endpoint-level validation — mirroring the platform-scope role check in the role service.
            var forbiddenPermission = desiredPermissions.FirstOrDefault(permission => !this.rolePermissionResolver.IsTenantGrantablePermission(permission));
            if (forbiddenPermission is not null)
            {
                throw new InvalidTenantAssignmentException(forbiddenPermission, $"Permission '{forbiddenPermission}' is not grantable within a tenant.");
            }

            var membershipPermissionSet = this.dbContext.Set<BlueprintTenantMembershipPermission<TUser, TTenant>>();

            var current = await membershipPermissionSet
                .Where(membershipPermission => membershipPermission.UserId == userId && membershipPermission.TenantId == tenantId)
                .ToListAsync(ct);

            var currentPermissions = current.Select(membershipPermission => membershipPermission.Permission).ToHashSet(StringComparer.Ordinal);

            var toRemove = current.Where(membershipPermission => !desiredPermissions.Contains(membershipPermission.Permission)).ToList();
            foreach (var membershipPermission in toRemove)
            {
                membershipPermissionSet.Remove(membershipPermission);
            }

            var toAdd = desiredPermissions.Where(permission => !currentPermissions.Contains(permission)).ToList();
            foreach (var permission in toAdd)
            {
                membershipPermissionSet.Add(new BlueprintTenantMembershipPermission<TUser, TTenant>
                {
                    UserId = userId,
                    TenantId = tenantId,
                    Permission = permission,
                });
            }

            await this.dbContext.SaveChangesAsync(ct);
        }
    }
}
