namespace Csag.Blueprint.Infrastructure.Database;

using System.Linq.Expressions;
using Csag.Blueprint.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;

/// <summary>
/// Extension methods for configuring soft deletion and active range filtering in Entity Framework Core models.
/// These extensions provide automatic global query filters for entities implementing soft deletion
/// and active range contracts.
/// <para>
/// Everything is configured at convention level, so explicit configuration of the same aspect (an
/// index name, for example) wins whether it runs before or after these methods.
/// </para>
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
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureSoftDeleteFiltering(modelBuilder.ConventionBuilder());
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
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureEntityFiltering(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Configures indexes for soft deletion columns to improve query performance.
    /// Creates an index on the DeletedAt column for entities implementing <see cref="ISoftDeletable"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance for method chaining.</returns>
    public static ModelBuilder ConfigureSoftDeleteIndexes(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureSoftDeleteIndexes(modelBuilder.ConventionBuilder());
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
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureActiveRangeIndexes(modelBuilder.ConventionBuilder());
        return modelBuilder;
    }

    /// <summary>
    /// Applies <see cref="ConfigureEntityFiltering(ModelBuilder)"/> through a convention-level builder.
    /// </summary>
    /// <param name="modelBuilder">The convention-level builder of the model to configure.</param>
    internal static void ConfigureEntityFiltering(IConventionModelBuilder modelBuilder)
    {
        ConfigureSoftDeleteFiltering(modelBuilder);
        ConfigureSoftDeleteIndexes(modelBuilder);
        ConfigureActiveRangeIndexes(modelBuilder);
    }

    /// <summary>
    /// Returns the convention-level builder of the model <paramref name="modelBuilder"/> is building.
    /// Configuring through it records each setting as a convention, ranked below anything the
    /// application configures explicitly or through data annotations.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The convention-level model builder.</returns>
    internal static IConventionModelBuilder ConventionBuilder(this ModelBuilder modelBuilder)
        => ((IConventionModel)modelBuilder.Model).Builder;

    /// <summary>
    /// Names <paramref name="index"/> <c>IX_{table}_{suffix}</c> after the table
    /// <paramref name="entityType"/> maps to. Two kinds of entity type keep EF Core's default name,
    /// which EF Core builds from the actual table and column names:
    /// <list type="bullet">
    /// <item>one with no table of its own, such as an abstract table-per-concrete-type root, whose
    /// index EF Core names separately for each concrete table;</item>
    /// <item>one that shares another entity type's rows, such as an owned type mapped to its owner's
    /// table, whose <c>IX_{table}_{suffix}</c> would collide with the owner's index of the same
    /// contract.</item>
    /// </list>
    /// </summary>
    /// <param name="index">The index to name, or <see langword="null"/> when it could not be configured.</param>
    /// <param name="entityType">The entity type the index is declared on.</param>
    /// <param name="suffix">The part of the name after the table name.</param>
    internal static void HasTableScopedName(this IConventionIndexBuilder? index, IReadOnlyEntityType entityType, string suffix)
    {
        var tableName = entityType.GetTableName();
        if (tableName == null
            || entityType.FindRowInternalForeignKeys(StoreObjectIdentifier.Table(tableName, entityType.GetSchema())).Any())
        {
            return;
        }

        index?.HasDatabaseName($"IX_{tableName}_{suffix}");
    }

    /// <summary>
    /// Registers one of the blueprint's named query filters on <paramref name="entityType"/>, and fails
    /// if a different filter already holds that name.
    /// </summary>
    /// <remarks>
    /// The <see cref="BlueprintQueryFilters"/> keys are reserved. Whichever of two filters with the same
    /// key is applied last wins, so an application filter registered under one of them would either
    /// replace the blueprint's (for tenant isolation, a cross-tenant leak) or be dropped without a trace.
    /// Configuration source cannot tell the two apart: EF Core's own model-finalizing conventions record
    /// an application's filters again at convention level before the blueprint conventions run. The
    /// expression can, because the blueprint's filters are deterministic, so applying the same
    /// configuration twice is still allowed.
    /// </remarks>
    /// <param name="entityType">The root entity type to filter.</param>
    /// <param name="filterKey">One of the <see cref="BlueprintQueryFilters"/> keys.</param>
    /// <param name="expression">The filter expression.</param>
    /// <param name="filterDescription">Human-readable name of the filter, used in the error message.</param>
    internal static void HasBlueprintQueryFilter(this IConventionEntityType entityType, string filterKey, LambdaExpression expression, string filterDescription)
    {
        var existing = entityType.FindDeclaredQueryFilter(filterKey)?.Expression;
        if (existing != null && !ExpressionEqualityComparer.Instance.Equals(existing, expression))
        {
            throw new InvalidOperationException(
                $"'{entityType.DisplayName()}' already has a query filter named '{filterKey}'. That name is " +
                $"reserved for the blueprint's {filterDescription} filter. Register your filter under a " +
                $"different name; filters with different names all apply.");
        }

        entityType.Builder.HasQueryFilter(filterKey, expression);
    }

    /// <summary>
    /// Sets the delete behavior of <paramref name="relationship"/> unless the application already chose
    /// one with the fluent API or a <c>[DeleteBehavior]</c> attribute.
    /// </summary>
    /// <remarks>
    /// The behavior is recorded at data-annotation level rather than as a convention: EF Core's
    /// cascade-delete convention reacts to a relationship created during model finalization only after
    /// finalization completes, and would overwrite a convention-level value.
    /// </remarks>
    /// <param name="relationship">The relationship to configure, or <see langword="null"/> when it could not be configured.</param>
    /// <param name="deleteBehavior">The delete behavior to apply.</param>
    internal static void OnDeleteUnlessConfigured(this IConventionForeignKeyBuilder? relationship, DeleteBehavior deleteBehavior)
    {
        if (relationship?.Metadata.GetDeleteBehaviorConfigurationSource() is ConfigurationSource.Explicit or ConfigurationSource.DataAnnotation)
        {
            return;
        }

        relationship?.OnDelete(deleteBehavior, fromDataAnnotation: true);
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

    private static void ConfigureSoftDeleteFiltering(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            // An owned type cannot carry a query filter; its rows are only reachable through the owner,
            // whose filter applies.
            if (entityType.IsOwned() || !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // A global query filter is only allowed on the root of an inheritance hierarchy, and a
            // derived type inherits the one its root carries.
            if (entityType.BaseType != null)
            {
                EnsureContractIsOnTheHierarchyRoot<ISoftDeletable>(entityType, "soft-delete");
                continue;
            }

            entityType.HasBlueprintQueryFilter(BlueprintQueryFilters.SoftDelete, BuildSoftDeleteFilter(entityType.ClrType), "soft-delete");
        }
    }

    private static void ConfigureSoftDeleteIndexes(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(DeclaresContract<ISoftDeletable>))
        {
            entityType.Builder
                .HasIndex([nameof(ISoftDeletable.DeletedAt)])
                .HasTableScopedName(entityType, "DeletedAt");
        }
    }

    private static void ConfigureActiveRangeIndexes(IConventionModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes().Where(DeclaresContract<IHasActiveRange>))
        {
            entityType.Builder
                .HasIndex([nameof(IHasActiveRange.ActiveFrom), nameof(IHasActiveRange.ActiveUntil)])
                .HasTableScopedName(entityType, "ActiveRange");
        }
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
