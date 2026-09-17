namespace Csag.Blueprint.TestHost.Endpoints.Auth.Login;

/// <summary>
/// Credentials for the password login endpoint.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant to sign in to. When omitted, the user's first tenant membership
    /// becomes the active tenant; when set, the user must be a member of that tenant.
    /// </summary>
    public Guid? TenantId { get; set; }
}
