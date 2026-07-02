namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Concrete opinionated implementation of <see cref="ITenantManager{TUser, TTenant}"/> backed by EF Core.
/// Analogous to Identity's <c>UserManager{TUser}</c> — generic, DI-friendly, persistence-backed.
/// </summary>
/// <typeparam name="TUser">The concrete user type.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type.</typeparam>
/// <typeparam name="TContext">The application's <see cref="DbContext"/> type.</typeparam>
#pragma warning disable S2436 // Three generic parameters are justified: TUser + TTenant (domain types) + TContext (persistence). Mirrors Identity's pattern.
public class TenantManager<TUser, TTenant, TContext> : ITenantManager<TUser, TTenant>
#pragma warning restore S2436
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
    where TContext : DbContext
{
    private readonly TContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantManager{TUser, TTenant, TContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    public TenantManager(TContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public Task<TenantOperationResult> CreateTenantAsync(
        TTenant tenant,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(userIds);

        return InternalAsync();

        async Task<TenantOperationResult> InternalAsync()
        {
            await using var transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);

            this.dbContext.Set<TTenant>().Add(tenant);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            await this.SyncMembersInternalAsync(tenant.Id, userIds, cancellationToken);

            await this.dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return TenantOperationResult.Success();
        }
    }

    /// <inheritdoc/>
    public Task<TenantOperationResult> UpdateTenantAsync(
        TTenant tenant,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(userIds);

        return InternalAsync();

        async Task<TenantOperationResult> InternalAsync()
        {
            await using var transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);

            this.dbContext.Set<TTenant>().Update(tenant);
            await this.dbContext.SaveChangesAsync(cancellationToken);

            await this.SyncMembersInternalAsync(tenant.Id, userIds, cancellationToken);

            await this.dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return TenantOperationResult.Success();
        }
    }

    /// <inheritdoc/>
    public Task<TenantOperationResult> SyncMembersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        return InternalAsync();

        async Task<TenantOperationResult> InternalAsync()
        {
            await this.SyncMembersInternalAsync(tenantId, userIds, cancellationToken);

            await this.dbContext.SaveChangesAsync(cancellationToken);
            return TenantOperationResult.Success();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BlueprintTenantMembership<TUser, TTenant>>> GetUserMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
            .Include(m => m.Tenant)
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TTenant?> GetUserTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var membership = await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken);

        if (membership == null)
        {
            return null;
        }

        return await this.dbContext.Set<TTenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TenantOperationResult> AddMemberAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var exists = await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
            .AnyAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken);

        if (exists)
        {
            return TenantOperationResult.Failure("User is already a member of this tenant.");
        }

        this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>().Add(new BlueprintTenantMembership<TUser, TTenant>
        {
            UserId = userId,
            TenantId = tenantId,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await this.dbContext.SaveChangesAsync(cancellationToken);
        return TenantOperationResult.Success();
    }

    /// <inheritdoc/>
    public async Task<TenantOperationResult> RemoveMemberAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var membership = await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken);

        if (membership == null)
        {
            return TenantOperationResult.Failure("User is not a member of this tenant.");
        }

        this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>().Remove(membership);
        await this.dbContext.SaveChangesAsync(cancellationToken);
        return TenantOperationResult.Success();
    }

    private async Task SyncMembersInternalAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var currentMemberships = await this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>()
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var currentUserIds = currentMemberships.Select(m => m.UserId).ToHashSet();
        var desiredUserIds = userIds.Distinct().ToHashSet();

        var membershipSet = this.dbContext.Set<BlueprintTenantMembership<TUser, TTenant>>();

        var toRemove = currentMemberships.Where(m => !desiredUserIds.Contains(m.UserId)).ToList();
        foreach (var membership in toRemove)
        {
            membershipSet.Remove(membership);
        }

        var toAdd = desiredUserIds.Except(currentUserIds).ToList();
        foreach (var userId in toAdd)
        {
            membershipSet.Add(new BlueprintTenantMembership<TUser, TTenant>
            {
                TenantId = tenantId,
                UserId = userId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }
    }
}
