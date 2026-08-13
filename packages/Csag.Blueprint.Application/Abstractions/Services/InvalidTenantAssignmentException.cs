namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Thrown by the tenant-scoped assignment services (<see cref="ITenantRoleService"/>,
/// <see cref="ITenantPermissionService"/>) when a requested role or permission value must not be
/// persisted as a tenant-scoped assignment: the role does not exist in the catalog, the role is
/// platform-scope, or the permission is not tenant-grantable. <see cref="InvalidName"/> carries just
/// the offending value so endpoints can echo it in a localized validation error without exposing the
/// server-side diagnostic message.
/// </summary>
public sealed class InvalidTenantAssignmentException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTenantAssignmentException"/> class.
    /// </summary>
    public InvalidTenantAssignmentException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTenantAssignmentException"/> class.
    /// </summary>
    /// <param name="message">The server-side diagnostic message.</param>
    public InvalidTenantAssignmentException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTenantAssignmentException"/> class.
    /// </summary>
    /// <param name="message">The server-side diagnostic message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public InvalidTenantAssignmentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTenantAssignmentException"/> class.
    /// </summary>
    /// <param name="invalidName">The offending role or permission value, safe to echo to the client.</param>
    /// <param name="message">The server-side diagnostic message.</param>
    public InvalidTenantAssignmentException(string invalidName, string message)
        : base(message)
    {
        this.InvalidName = invalidName;
    }

    /// <summary>
    /// Gets the offending role or permission value. Client-safe: it is the caller's own input, without
    /// any server-side context around it.
    /// </summary>
    public string? InvalidName { get; }
}
