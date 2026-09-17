namespace Csag.Blueprint.Infrastructure.Database;

using System.Linq.Expressions;
using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // A global query filter is only allowed on the root of an inheritance hierarchy, and a
            // derived type already inherits the one its root carries. Registering a filter for every
            // mapped type made model validation fail outright for any consumer using TPH or TPT.
            if (entityType.BaseType != null)
            {
                EnsureContractIsOnTheHierarchyRoot<ISoftDeletable>(entityType, "soft-delete");
                continue;
            }

            var filter = BuildSoftDeleteFilter(entityType.ClrType);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(BlueprintQueryFilters.SoftDelete, filter);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures the global query filters that make sense to apply unconditionally — currently soft
    /// deletion only — together with the indexes backing them.
    /// <para>
    /// <see cref="IHasActiveRange"/> is deliberately <b>not</b> turned into a global filter: an active
    /// range is evaluated against a point in time chosen by the caller, and "now" is only one of them.
    /// Availability searches look at a future window and administrators legitimately need to see and
    /// edit entities that are not active yet or no longer active. Use the explicit
    /// <c>WhereActiveNow()</c> / <c>WhereActiveAt()</c> / <c>WhereActiveInRange()</c> query extensions instead.
    /// Its index is still created, because those extensions are the expected access path.
    /// </para>
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureEntityFiltering(this ModelBuilder modelBuilder)
    {
        return modelBuilder
            .ConfigureSoftDeleteFiltering()
            .ConfigureSoftDeleteIndexes()
            .ConfigureActiveRangeIndexes();
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
            if (DeclaresContract<ISoftDeletable>(entityType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(ISoftDeletable.DeletedAt))
                    .HasDatabaseName($"IX_{entityType.GetTableName()}_DeletedAt");
            }
        }

        return modelBuilder;
    }

    /// <summary>
    /// Configures the composite index backing the active range queries for entities implementing
    /// <see cref="IHasActiveRange"/>. A single <c>(ActiveFrom, ActiveUntil)</c> index also serves
    /// lookups on <c>ActiveFrom</c> alone, so no separate single-column indexes are created.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureActiveRangeIndexes(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (DeclaresContract<IHasActiveRange>(entityType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(IHasActiveRange.ActiveFrom), nameof(IHasActiveRange.ActiveUntil))
                    .HasDatabaseName($"IX_{entityType.GetTableName()}_ActiveRange");
            }
        }

        return modelBuilder;
    }

    /// <summary>
    /// Determines whether <paramref name="entityType"/> is the topmost type in its hierarchy to
    /// implement <typeparamref name="TContract"/>, and therefore the one that should carry the
    /// contract's index.
    /// </summary>
    /// <remarks>
    /// In a TPH hierarchy every mapped type reports the same table name, so indexing each of them
    /// redefines the same index over and over. In a TPT hierarchy a contract adopted only by a derived
    /// type belongs on that derived type's own table, which this still allows.
    /// </remarks>
    /// <typeparam name="TContract">The domain contract.</typeparam>
    /// <param name="entityType">The entity type to test.</param>
    /// <returns><see langword="true"/> when the contract is declared at this level of the hierarchy.</returns>
    internal static bool DeclaresContract<TContract>(IReadOnlyEntityType entityType)
        => typeof(TContract).IsAssignableFrom(entityType.ClrType)
            && (entityType.BaseType == null || !typeof(TContract).IsAssignableFrom(entityType.BaseType.ClrType));

    /// <summary>
    /// Guards the one inheritance shape a global query filter cannot express: a derived type adopting
    /// the contract while its hierarchy root does not.
    /// </summary>
    /// <remarks>
    /// A derived type inherits its root's filter, so there is nothing to register when the root also
    /// implements the contract. When it does not, EF Core offers nowhere to put the filter — and
    /// silently skipping would leave those rows unfiltered, which for soft delete or tenancy means
    /// returning data that should not be visible. Failing at model build is the safer outcome.
    /// </remarks>
    /// <typeparam name="TContract">The domain contract.</typeparam>
    /// <param name="entityType">The derived entity type implementing the contract.</param>
    /// <param name="filterDescription">Human-readable name of the filter, used in the error message.</param>
    internal static void EnsureContractIsOnTheHierarchyRoot<TContract>(IReadOnlyEntityType entityType, string filterDescription)
    {
        var root = entityType.GetRootType();
        if (typeof(TContract).IsAssignableFrom(root.ClrType))
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{entityType.ClrType.Name}' implements {typeof(TContract).Name}, but its inheritance root " +
            $"'{root.ClrType.Name}' does not. EF Core only allows a global query filter on the root of a " +
            $"hierarchy, so the {filterDescription} filter cannot be applied to '{entityType.ClrType.Name}'. " +
            $"Move {typeof(TContract).Name} up to '{root.ClrType.Name}', or map '{entityType.ClrType.Name}' " +
            $"outside this hierarchy.");
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
