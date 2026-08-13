namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantRoleService"/>. Reads and writes
/// <see cref="BlueprintTenantMembershipRole{TUser,TTenant}"/> rows, mapping between role names
/// (from the shared <c>AspNetRoles</c> catalog) and role identifiers.
/// </summary>
/// <typeparam name="TUser">The concrete user type.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type.</typeparam>
/// <typeparam name="TContext">The application's <see cref="DbContext"/> type.</typeparam>
#pragma warning disable S2436 // Three generic parameters mirror the persistence-backed manager pattern used across the Blueprint packages.
public sealed class TenantRoleService<TUser, TTenant, TContext> : ITenantRoleService
#pragma warning restore S2436
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
    where TContext : DbContext
{
    private readonly TContext dbContext;
    private readonly ILookupNormalizer lookupNormalizer;
    private readonly IRolePermissionResolver rolePermissionResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantRoleService{TUser, TTenant, TContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    /// <param name="lookupNormalizer">The Identity lookup normalizer used to match role names to their normalized catalog values.</param>
    /// <param name="rolePermissionResolver">Resolver used to reject platform-scope roles, which must never be assigned within a tenant.</param>
    public TenantRoleService(TContext dbContext, ILookupNormalizer lookupNormalizer, IRolePermissionResolver rolePermissionResolver)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.lookupNormalizer = lookupNormalizer ?? throw new ArgumentNullException(nameof(lookupNormalizer));
        this.rolePermissionResolver = rolePermissionResolver ?? throw new ArgumentNullException(nameof(rolePermissionResolver));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        return await this.dbContext.Set<BlueprintTenantMembershipRole<TUser, TTenant>>()
            .AsNoTracking()
            .Where(membershipRole => membershipRole.UserId == userId && membershipRole.TenantId == tenantId)
            .Join(
                this.dbContext.Set<BlueprintRole>(),
                membershipRole => membershipRole.RoleId,
                role => role.Id,
                (membershipRole, role) => role.Name!)
            .OrderBy(name => name)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public Task SetRolesAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> roleNames, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roleNames);

        return SetRolesInternalAsync();

        async Task SetRolesInternalAsync()
        {
            // Role rows hang off the tenant membership (their FK cascades from it). Verify the membership
            // inside the write path so a violated precondition — a caller that skipped the membership, or a
            // concurrent membership removal — surfaces as a clear error instead of a raw FK violation from
            // SaveChanges.
            var isMember = await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
                .AnyAsync(membership => membership.UserId == userId && membership.TenantId == tenantId, ct);
            if (!isMember)
            {
                throw new TenantMembershipRequiredException($"User '{userId}' is not a member of tenant '{tenantId}'; tenant-scoped roles require an existing membership.");
            }

            var desiredRoleIds = await this.ResolveRoleIdsAsync(roleNames, ct);

            var membershipRoleSet = this.dbContext.Set<BlueprintTenantMembershipRole<TUser, TTenant>>();

            var current = await membershipRoleSet
                .Where(membershipRole => membershipRole.UserId == userId && membershipRole.TenantId == tenantId)
                .ToListAsync(ct);

            var currentRoleIds = current.Select(membershipRole => membershipRole.RoleId).ToHashSet();

            var toRemove = current.Where(membershipRole => !desiredRoleIds.Contains(membershipRole.RoleId)).ToList();
            foreach (var membershipRole in toRemove)
            {
                membershipRoleSet.Remove(membershipRole);
            }

            var toAdd = desiredRoleIds.Where(roleId => !currentRoleIds.Contains(roleId)).ToList();
            foreach (var roleId in toAdd)
            {
                membershipRoleSet.Add(new BlueprintTenantMembershipRole<TUser, TTenant>
                {
                    UserId = userId,
                    TenantId = tenantId,
                    RoleId = roleId,
                });
            }

            await this.dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task<HashSet<Guid>> ResolveRoleIdsAsync(IReadOnlyCollection<string> roleNames, CancellationToken ct)
    {
        var distinctNames = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctNames.Count == 0)
        {
            return [];
        }

        // Map each requested name to its normalized form, skipping any the normalizer cannot produce.
        var normalizedByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in distinctNames)
        {
            var normalized = this.lookupNormalizer.NormalizeName(name);
            if (!string.IsNullOrEmpty(normalized))
            {
                normalizedByName[name] = normalized;
            }
        }

        var normalizedNames = normalizedByName.Values.ToList();

        var catalog = await this.dbContext.Set<BlueprintRole>()
            .AsNoTracking()
            .Where(role => role.NormalizedName != null && normalizedNames.Contains(role.NormalizedName))
            .Select(role => new { role.Id, Name = role.Name!, NormalizedName = role.NormalizedName! })
            .ToListAsync(ct);

        var catalogByNormalizedName = catalog.ToDictionary(role => role.NormalizedName, role => role, StringComparer.Ordinal);

        var resolved = new HashSet<Guid>();
        foreach (var name in distinctNames)
        {
            if (!normalizedByName.TryGetValue(name, out var normalized) ||
                !catalogByNormalizedName.TryGetValue(normalized, out var role))
            {
                throw new InvalidTenantAssignmentException(name, $"Role '{name}' does not exist in the role catalog.");
            }

            // Platform-scope roles are assigned globally and must never be persisted as tenant-scoped
            // assignments — a tenant-scoped platform role would confer platform-wide capabilities at
            // authentication time. This enforces the invariant at the persistence seam, independent of
            // any endpoint-level validation. The check runs on the CANONICAL catalog name: the lookup
            // above is case-insensitive (normalized names), so guarding only the caller's raw spelling
            // would let a case variant resolve to a platform role.
            if (this.rolePermissionResolver.IsPlatformScopeRole(role.Name))
            {
                throw new InvalidTenantAssignmentException(role.Name, $"Role '{role.Name}' is platform-scope and cannot be assigned within a tenant.");
            }

            resolved.Add(role.Id);
        }

        return resolved;
    }
}
