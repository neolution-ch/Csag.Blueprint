namespace Csag.Blueprint.Infrastructure.Database;

using System.Linq.Expressions;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for configuring multi-tenancy in Entity Framework Core models.
/// Owned by the Blueprint packages — applications call these methods from their <c>OnModelCreating</c>.
/// </summary>
public static class MultiTenancyModelBuilderExtensions
{
    /// <summary>
    /// Configures multi-tenancy for all entities implementing <see cref="IMustHaveTenant"/>.
    /// Applies a named global query filter (<see cref="BlueprintQueryFilters.Tenant"/>) tied to the
    /// <paramref name="context"/> instance and creates indexes on <c>TenantId</c>. Also establishes
    /// foreign key relationships to the tenant table.
    /// The filter fails closed: when no ambient tenant is set, tenant-owned queries return no rows;
    /// use <c>IgnoreQueryFilters()</c> for deliberate cross-tenant access.
    /// </summary>
    /// <typeparam name="TTenant">The concrete tenant type (must derive from <see cref="BlueprintTenant"/>).</typeparam>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type that exposes <c>CurrentTenantId</c>.</typeparam>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="context">
    /// The <typeparamref name="TContext"/> instance from <c>OnModelCreating</c>. EF Core's
    /// <c>ParameterExtractingExpressionVisitor</c> detects a <see cref="DbContext"/>-typed constant
    /// and re-evaluates it against the current executing context per query.
    /// </param>
    /// <param name="currentTenantIdPropertyName">
    /// The name of the <c>Guid?</c> instance property on <typeparamref name="TContext"/> that
    /// returns the current tenant ID. Defaults to <c>"CurrentTenantId"</c>.
    /// </param>
    /// <param name="addTenantForeignKey">
    /// Whether to add a foreign key from each tenant-owned entity to the tenant table. Defaults to
    /// <see langword="true"/>, which is correct for the default pooled topology (everything in one
    /// database).
    /// <para>
    /// Set this to <see langword="false"/> when tenant-owned <b>business</b> data lives in a different
    /// database from the tenant table (a database-per-tenant or split-plane topology). A foreign key
    /// cannot cross databases, so emitting one there produces an invalid model. The query filter and
    /// the <c>TenantId</c> index are still applied; only referential integrity moves from the database
    /// to the application.
    /// </para>
    /// </param>
    public static void ConfigureBlueprintMultiTenancy<TTenant, TContext>(
        this ModelBuilder modelBuilder,
        TContext context,
        string currentTenantIdPropertyName = "CurrentTenantId",
        bool addTenantForeignKey = true)
        where TTenant : BlueprintTenant
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        var contextType = typeof(TContext);
        var currentTenantIdProp = contextType.GetProperty(currentTenantIdPropertyName)
            ?? throw new InvalidOperationException(
                $"Property '{currentTenantIdPropertyName}' not found on {contextType.Name}. " +
                $"Ensure your DbContext exposes a public Guid? instance property for the current tenant ID.");

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            // EF Core allows a global query filter only on the root of an inheritance hierarchy, and a
            // derived type inherits the root's. Registering one per mapped type made any consumer with a
            // TPH/TPT hierarchy fail model validation outright.
            if (entityType.BaseType != null)
            {
                EntityFilteringModelBuilderExtensions.EnsureContractIsOnTheHierarchyRoot<IMustHaveTenant>(entityType, "tenant isolation");
                continue;
            }

            ApplyGlobalQueryFilter(modelBuilder, entityType.ClrType, context, contextType, currentTenantIdProp);
        }

        foreach (var entityType in entityTypes.Where(EntityFilteringModelBuilderExtensions.DeclaresContract<IMustHaveTenant>))
        {
            // The index and foreign key follow the column, which in a TPH hierarchy exists once on the
            // shared table — so they belong on the topmost type that declares the contract, not on
            // every mapped type in the hierarchy.
            modelBuilder.Entity(entityType.ClrType)
                .HasIndex(nameof(IMustHaveTenant.TenantId));

            if (addTenantForeignKey)
            {
                AddTenantForeignKey<TTenant>(modelBuilder, entityType.ClrType);
            }
        }
    }

    private static void ApplyGlobalQueryFilter<TContext>(
        ModelBuilder modelBuilder,
        Type entityType,
        TContext context,
        Type contextType,
        System.Reflection.PropertyInfo currentTenantIdProp)
        where TContext : DbContext
    {
        var parameter = Expression.Parameter(entityType, "e");
        var tenantIdProperty = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));

        var contextConstant = Expression.Constant(context, contextType);
        var currentTenantId = Expression.Property(contextConstant, currentTenantIdProp);

        // Build "e => (Guid?)e.TenantId == context.CurrentTenantId", keeping the comparison in the
        // lifted nullable form. EF funcletizes the right side into a nullable query parameter, so
        // with no ambient tenant the filter compares TenantId against NULL — which matches no rows.
        // That makes the no-tenant case fail closed with a deterministic empty result; callers that
        // really need cross-tenant access must opt out via IgnoreQueryFilters(). Accessing ".Value"
        // here instead would be funcletized eagerly and throw at query time whenever the ambient
        // tenant is missing. The Convert is required because expression trees do not lift operand
        // types the way the C# compiler does.
        var liftedTenantId = Expression.Convert(tenantIdProperty, currentTenantIdProp.PropertyType);
        var filterExpression = Expression.Equal(liftedTenantId, currentTenantId);
        var lambda = Expression.Lambda(filterExpression, parameter);

        // Registered as a named filter so unrelated opt-outs (e.g. soft delete) cannot drop tenant isolation.
        modelBuilder.Entity(entityType).HasQueryFilter(BlueprintQueryFilters.Tenant, lambda);
    }

    private static void AddTenantForeignKey<TTenant>(ModelBuilder modelBuilder, Type entityType)
        where TTenant : BlueprintTenant
    {
        modelBuilder.Entity(entityType)
            .HasOne(typeof(TTenant))
            .WithMany()
            .HasForeignKey(nameof(IMustHaveTenant.TenantId))
            .OnDelete(DeleteBehavior.Restrict);
    }
}
