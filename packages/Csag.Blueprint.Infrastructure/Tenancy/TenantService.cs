namespace Csag.Blueprint.Infrastructure.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Services;

/// <summary>
/// Service for retrieving the current tenant context.
/// Reads from TenantContext (ambient AsyncLocal storage) which is set by middleware from claims.
/// This provides a consistent tenant context across the entire request pipeline.
/// </summary>
public sealed class TenantService : ITenantService
{
    /// <inheritdoc/>
    public Guid? CurrentTenantId => TenantContext.Current;
}
