namespace Csag.Blueprint.TestHost.Endpoints.Auth.SwitchTenant;

/// <summary>
/// The tenant to move the caller's session to.
/// </summary>
public sealed class SwitchTenantRequest
{
    /// <summary>
    /// Gets or sets the target tenant.
    /// </summary>
    public Guid TenantId { get; set; }
}
