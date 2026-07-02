namespace Csag.Blueprint.Application.Abstractions.Services;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// Manages tenant and membership lifecycle operations.
/// Analogous to Identity's <c>UserManager{TUser}</c> — a DI-friendly,
/// generic, persistence-backed service with a result-based API.
/// </summary>
/// <typeparam name="TUser">The concrete user type, must derive from <see cref="BlueprintUser"/>.</typeparam>
/// <typeparam name="TTenant">The concrete tenant type, must derive from <see cref="BlueprintTenant"/>.</typeparam>
public interface ITenantManager<TUser, TTenant>
    where TUser : BlueprintUser
    where TTenant : BlueprintTenant
{
    /// <summary>
    /// Persists a prepared tenant entity and synchronises its memberships as one atomic workflow.
    /// </summary>
    /// <param name="tenant">The prepared tenant entity to create.</param>
    /// <param name="userIds">The desired set of user IDs for tenant membership.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<TenantOperationResult> CreateTenantAsync(TTenant tenant, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updates to a prepared tenant entity and synchronises its memberships as one atomic workflow.
    /// </summary>
    /// <param name="tenant">The prepared tenant entity to update.</param>
    /// <param name="userIds">The desired set of user IDs for tenant membership.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<TenantOperationResult> UpdateTenantAsync(TTenant tenant, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all memberships for a user, including the related tenant entity.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An ordered list of memberships with their tenant and user navigations loaded.</returns>
    Task<IReadOnlyList<BlueprintTenantMembership<TUser, TTenant>>> GetUserMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the tenant for a user, or null if the user is not a member.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant if the user is a member; otherwise <c>null</c>.</returns>
    Task<TTenant?> GetUserTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user to a tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<TenantOperationResult> AddMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<TenantOperationResult> RemoveMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronises the membership list for a tenant to exactly the given set of user IDs.
    /// Users not in <paramref name="userIds"/> are removed; users not yet members are added.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userIds">The desired set of user IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<TenantOperationResult> SyncMembersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
