namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Answers whether a user is a member of a tenant, without the user and tenant type parameters
/// <see cref="ITenantManager{TUser, TTenant}"/> carries, so services that are not generic over the tenant type can
/// ask. <c>AddBlueprintTenantManager</c> registers it as the application's tenant manager.
/// </summary>
public interface ITenantMembershipChecker
{
    /// <summary>
    /// Determines whether a user is a member of a tenant. Does not load the tenant entity.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the user is a member of the tenant; otherwise <c>false</c>.</returns>
    Task<bool> IsMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
