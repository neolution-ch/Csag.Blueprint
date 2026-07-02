namespace Csag.Blueprint.Infrastructure.Database.Interceptors;

using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Interceptor that automatically manages audit timestamps on <see cref="IAuditable"/> entities.
/// Sets <see cref="IAuditable.CreatedAt"/> on insert and <see cref="IAuditable.UpdatedAt"/> on update.
/// </summary>
public sealed class AuditableTimestampInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            SetAuditTimestamps(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            SetAuditTimestamps(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void SetAuditTimestamps(DbContext context)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
            else
            {
                // Other states (Unchanged, Detached, Deleted) require no timestamp action
            }
        }
    }
}
