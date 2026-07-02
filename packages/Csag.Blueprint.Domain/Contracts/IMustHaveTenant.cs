namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface for entities that must belong to a tenant.
/// Implementing this interface enables automatic tenant filtering and isolation.
/// </summary>
public interface IMustHaveTenant
{
    /// <summary>
    /// Gets or sets the tenant identifier that owns this entity.
    /// </summary>
    Guid TenantId { get; set; }
}
