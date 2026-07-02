namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Service for retrieving the current tenant context from the HTTP request.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets the current tenant identifier from the authenticated user's claims.
    /// </summary>
    /// <returns>The current tenant ID, or null if no tenant context is available.</returns>
    Guid? CurrentTenantId { get; }
}
