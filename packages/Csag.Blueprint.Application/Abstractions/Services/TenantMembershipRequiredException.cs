namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Thrown by the tenant-scoped assignment services (<see cref="ITenantRoleService"/>,
/// <see cref="ITenantPermissionService"/>) when the target user has no membership in the target
/// tenant. Tenant-scoped assignments hang off the membership row, so this signals a violated
/// precondition: the caller skipped the membership, or a concurrent removal deleted it between the
/// caller's check and the write. Endpoints treat it as "target not found in this tenant" (their 404
/// contract) — never surface the exception message to clients.
/// </summary>
public sealed class TenantMembershipRequiredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMembershipRequiredException"/> class.
    /// </summary>
    public TenantMembershipRequiredException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMembershipRequiredException"/> class.
    /// </summary>
    /// <param name="message">The server-side diagnostic message.</param>
    public TenantMembershipRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMembershipRequiredException"/> class.
    /// </summary>
    /// <param name="message">The server-side diagnostic message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public TenantMembershipRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
