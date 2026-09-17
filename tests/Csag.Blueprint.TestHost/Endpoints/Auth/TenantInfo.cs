namespace Csag.Blueprint.TestHost.Endpoints.Auth;

/// <summary>
/// A tenant the signed-in user is a member of.
/// </summary>
public sealed class TenantInfo
{
    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the tenant display name.
    /// </summary>
    public string TenantName { get; set; } = string.Empty;
}
