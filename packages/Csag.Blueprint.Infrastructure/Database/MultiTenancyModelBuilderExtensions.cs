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
    /// Applies global query filters tied to the <paramref name="context"/> instance and creates
    /// indexes on <c>TenantId</c>. Also establishes foreign key relationships to the tenant table.
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
            .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType));

        foreach (var entityType in entityTypes)
        {
            ApplyGlobalQueryFilter(modelBuilder, entityType.ClrType, context, contextType, currentTenantIdProp);

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
        var hasValueProperty = Expression.Property(currentTenantId, "HasValue");
        var currentTenantIdValue = Expression.Property(currentTenantId, "Value");

        var equalExpression = Expression.Equal(tenantIdProperty, currentTenantIdValue);
        var filterExpression = Expression.AndAlso(hasValueProperty, equalExpression);
        var lambda = Expression.Lambda(filterExpression, parameter);

        modelBuilder.Entity(entityType).HasQueryFilter(lambda);
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
