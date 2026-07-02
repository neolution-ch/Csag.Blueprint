namespace Csag.Blueprint.Infrastructure.Database.Interceptors;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Interceptor that automatically manages tenant assignment on entity operations.
/// Ensures tenant isolation by:
/// - Automatically setting TenantId when adding new entities from TenantContext (ambient AsyncLocal)
/// - Preventing TenantId modification on existing entities
/// </summary>
public sealed class TenantSaveInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        HandleTenantAssignment(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            HandleTenantAssignment(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void HandleTenantAssignment(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentTenantId = TenantContext.Current;

        foreach (var entry in context.ChangeTracker.Entries<IMustHaveTenant>())
        {
            if (entry.State == EntityState.Added)
            {
                // Set TenantId on new entities
                if (currentTenantId.HasValue)
                {
                    entry.Entity.TenantId = currentTenantId.Value;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot add entity of type {entry.Entity.GetType().Name} without a valid tenant. " +
                        $"Ensure tenant {nameof(context)} is set via TenantContext.SetTenant() or user has valid TenantId claim.");
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Prevent TenantId modification
                var tenantIdProperty = entry.Property(nameof(IMustHaveTenant.TenantId));

                if (tenantIdProperty.IsModified)
                {
                    throw new InvalidOperationException(
                        $"Cannot modify TenantId for entity of type {entry.Entity.GetType().Name}. " +
                        "Moving entities between tenants is not allowed.");
                }
            }
            else
            {
                // No action needed for Unchanged, Deleted, or Detached states
            }
        }
    }
}
