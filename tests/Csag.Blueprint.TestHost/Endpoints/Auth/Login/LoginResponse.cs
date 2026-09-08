namespace Csag.Blueprint.TestHost.Endpoints.Auth.Login;

/// <summary>
/// The authenticated user's session summary returned by a successful login.
/// </summary>
public sealed class LoginResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether a full session was established.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the effective role names on the session principal (platform-scope global roles
    /// plus tenant-scoped roles for the active tenant).
    /// </summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the effective permissions on the session principal.
    /// </summary>
    public IList<string> Permissions { get; set; } = [];

    /// <summary>
    /// Gets or sets the active tenant of the session, or null when the user has no memberships.
    /// </summary>
    public Guid? CurrentTenantId { get; set; }

    /// <summary>
    /// Gets or sets all tenants the user is a member of.
    /// </summary>
    public IList<TenantInfo> AvailableTenants { get; set; } = [];
}
