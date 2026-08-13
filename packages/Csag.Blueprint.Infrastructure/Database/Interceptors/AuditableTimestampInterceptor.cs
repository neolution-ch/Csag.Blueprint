namespace Csag.Blueprint.Infrastructure.Database.Interceptors;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Interceptor that automatically manages audit stamps on <see cref="IAuditable"/> entities.
/// Sets <see cref="IAuditable.CreatedAt"/> / <see cref="IAuditable.CreatedByActor"/> on insert and
/// <see cref="IAuditable.UpdatedAt"/> / <see cref="IAuditable.UpdatedByActor"/> on update.
/// <para>
/// The acting actor label is read from <see cref="CurrentActorContext"/> (an AsyncLocal-backed ambient value),
/// which is why this interceptor is safe to register as a singleton / shared across pooled DbContext
/// instances — it captures no scoped dependency. When there is no current actor (data seeding, background
/// services, migrations, unauthenticated requests) the <c>CreatedByActor</c> / <c>UpdatedByActor</c> columns
/// are left null.
/// </para>
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
            SetAuditStamps(eventData.Context);
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
            SetAuditStamps(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void SetAuditStamps(DbContext context)
    {
        var now = DateTimeOffset.UtcNow;

        // Ambient AsyncLocal value; null when there is no acting actor (seeding, background services,
        // migrations, unauthenticated requests) — in that case the *ByActor columns are left null.
        var currentActor = CurrentActorContext.Current;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedByActor = currentActor;

                // Mirror the timestamp logic exactly: only Created* is set on insert.
                // UpdatedAt / UpdatedByActor stay null until the row is first modified, keeping the
                // "updated" pair semantically consistent (both populated together on update).
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedByActor = currentActor;

                // Never overwrite CreatedByActor on update (mirrors CreatedAt not being touched here).
            }
            else
            {
                // Other states (Unchanged, Detached, Deleted) require no audit action
            }
        }
    }
}
