namespace Csag.Blueprint.TestHost.Extensions;

using Audit.EntityFramework;
using Csag.Blueprint.Infrastructure.Database.Interceptors;
using Csag.Blueprint.Infrastructure.Tenancy;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.Extensions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Registers the host's persistence layer: the <see cref="TestDbContext"/> against SQL Server with
/// the Blueprint save interceptors, plus the shared tenancy runtime services.
/// </summary>
public static class TestHostPersistenceExtensions
{
    /// <summary>
    /// Adds the pooled <see cref="TestDbContext"/> factory (with audit, auditable-timestamp, and
    /// tenant save interceptors), a scoped context created from that factory, and the Blueprint
    /// tenancy runtime and tenant manager closures.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance providing the connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestHostPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Fails fast when ConnectionStrings:Default is missing so a misconfigured host does not
        // surface as an opaque DI failure later.
        _ = configuration.GetValidatedDatabaseOptions();
        var connectionString = configuration.GetConnectionString("Default")!;

        services.AddBlueprintTenancyRuntime();
        services.AddBlueprintTenantManager<TestUser, TestTenant, TestDbContext>();

        // IHttpContextAccessor is required by the audit configuration to enrich audit events with
        // the current user identity and correlation ID.
        services.AddHttpContextAccessor();

        // Single pooled DbContextFactory carrying all save interceptors. The tenant interceptor is
        // the singleton registered by AddBlueprintTenancyRuntime; the audit and timestamp
        // interceptors are stateless (or keyed by context instance) and safe to share across the pool.
        services.AddPooledDbContextFactory<TestDbContext>((sp, options) =>
        {
            var tenantInterceptor = sp.GetRequiredService<TenantSaveInterceptor>();
            options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure())
                .AddInterceptors(new AuditSaveChangesInterceptor(), new AuditableTimestampInterceptor(), tenantInterceptor);
        });

        // Scoped context for request handlers, created from the factory so it inherits the
        // interceptors; the scope disposes it, returning the instance to the pool.
        services.AddScoped<TestDbContext>(sp => sp.GetRequiredService<IDbContextFactory<TestDbContext>>().CreateDbContext());

        return services;
    }
}
