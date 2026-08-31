namespace Csag.Blueprint.Infrastructure.Database;

using System.Linq.Expressions;
using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for configuring soft deletion and active range filtering in Entity Framework Core models.
/// These extensions provide automatic global query filters for entities implementing soft deletion
/// and active range contracts.
/// </summary>
public static class EntityFilteringModelBuilderExtensions
{
    /// <summary>
    /// Configures automatic soft deletion filtering for all entities implementing <see cref="ISoftDeletable"/>.
    /// Applies a named global query filter (<see cref="BlueprintQueryFilters.SoftDelete"/>) that excludes
    /// soft deleted entities from all queries. Because the filter is named, a query can opt out of it
    /// alone via <c>IgnoreSoftDeleteFilter()</c> without also losing tenant isolation.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureSoftDeleteFiltering(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var filter = BuildSoftDeleteFilter(entityType.ClrType);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(BlueprintQueryFilters.SoftDelete, filter);
            }
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures the global query filters that make sense to apply unconditionally — currently soft
    /// deletion only.
    /// <para>
    /// <see cref="IHasActiveRange"/> is deliberately <b>not</b> turned into a global filter: an active
    /// range is evaluated against a point in time chosen by the caller, and "now" is only one of them.
    /// Availability searches look at a future window and administrators legitimately need to see and
    /// edit entities that are not active yet or no longer active. Use the explicit
    /// <c>WhereActiveNow()</c> / <c>WhereActiveAt()</c> / <c>WhereActiveInRange()</c> query extensions instead.
    /// </para>
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureEntityFiltering(this ModelBuilder modelBuilder)
    {
        return modelBuilder
            .ConfigureSoftDeleteFiltering();
    }

    /// <summary>
    /// Configures indexes for soft deletion columns to improve query performance.
    /// Creates an index on the DeletedAt column for entities implementing <see cref="ISoftDeletable"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureSoftDeleteIndexes(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(ISoftDeletable.DeletedAt))
                    .HasDatabaseName($"IX_{entityType.GetTableName()}_DeletedAt");
            }
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures indexes for active range columns to improve query performance.
    /// Creates indexes on the ActiveFrom and ActiveUntil columns for entities implementing <see cref="IHasActiveRange"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureActiveRangeIndexes(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IHasActiveRange).IsAssignableFrom(entityType.ClrType))
            {
                var tableName = entityType.GetTableName();

                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(IHasActiveRange.ActiveFrom))
                    .HasDatabaseName($"IX_{tableName}_ActiveFrom");

                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(IHasActiveRange.ActiveUntil))
                    .HasDatabaseName($"IX_{tableName}_ActiveUntil");

                // Composite index for better range query performance
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(IHasActiveRange.ActiveFrom), nameof(IHasActiveRange.ActiveUntil))
                    .HasDatabaseName($"IX_{tableName}_ActiveRange");
            }
        }

        return modelBuilder;
    }

    private static LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var deletedAtProperty = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
        var nullConstant = Expression.Constant(null, typeof(DateTimeOffset?));
        var equalExpression = Expression.Equal(deletedAtProperty, nullConstant);

        return Expression.Lambda(equalExpression, parameter);
    }
}
