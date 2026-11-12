namespace Csag.Blueprint.Realtime.AzureSignalR;

/// <summary>
/// Builds the addressing strings used by the real-time channel.
/// </summary>
/// <remarks>
/// This is the single source of truth shared by the path that grants group membership and the path
/// that publishes to a group. Keeping both on these helpers is what stops the two from drifting —
/// a mismatch would not fail a build or a test, it would silently deliver nothing.
/// </remarks>
public static class RealtimeGroupNames
{
    /// <summary>
    /// Builds the group name carrying every connection belonging to a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The group name, comfortably inside the service limit of 1024 characters.</returns>
    public static string Tenant(Guid tenantId)
    {
        return $"t:{tenantId:N}";
    }

    /// <summary>
    /// Builds the user identifier registered with the real-time service.
    /// </summary>
    /// <remarks>
    /// The tenant is part of the identifier rather than only of the group name. A person may belong
    /// to several tenants with different roles in each, so a bare user identifier would let a
    /// user-scoped message reach a session that has switched to a different tenant.
    /// </remarks>
    /// <param name="tenantId">The tenant the session is scoped to.</param>
    /// <param name="userId">The application user identifier.</param>
    /// <returns>The tenant-qualified user identifier.</returns>
    public static string ServiceUserId(Guid tenantId, Guid userId)
    {
        return $"{tenantId:N}:{userId:N}";
    }
}
