namespace Csag.Blueprint.Tests.Shared.Helpers;

using System.Globalization;
using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Factory for creating <see cref="TestDbContext"/> instances for testing.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// A fixed tenant ID used by unit tests for multi-tenancy query filters.
    /// </summary>
    public static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001", CultureInfo.InvariantCulture);

    /// <summary>
    /// Creates a <see cref="TestDbContext"/> configured with an in-memory database.
    /// Each call creates a new database instance with a unique name.
    /// Sets TenantContext.Current to <see cref="TestTenantId"/> so that
    /// global query filters can evaluate without throwing.
    /// </summary>
    /// <param name="interceptors">Optional interceptors to register on the context options (e.g. the save interceptors under test).</param>
    /// <returns>A disposable wrapper containing the <see cref="TestDbContext"/>.</returns>
    public static TestDbContextScope<TestDbContext> CreateInMemoryDbContext(params IInterceptor[] interceptors)
    {
        return CreateInMemoryDbContext(TestTenantId, interceptors);
    }

    /// <summary>
    /// Creates a <see cref="TestDbContext"/> configured with an in-memory database.
    /// Each call creates a new database instance with a unique name.
    /// Sets TenantContext.Current to <paramref name="tenantId"/> so that
    /// global query filters can evaluate without throwing.
    /// </summary>
    /// <param name="tenantId">The tenant ID to set as the ambient tenant before the context is created.</param>
    /// <param name="interceptors">Optional interceptors to register on the context options (e.g. the save interceptors under test).</param>
    /// <returns>A disposable wrapper containing the <see cref="TestDbContext"/>.</returns>
    public static TestDbContextScope<TestDbContext> CreateInMemoryDbContext(Guid tenantId, params IInterceptor[] interceptors)
    {
        TenantContext.SetTenant(tenantId);

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var context = new TestDbContext(optionsBuilder.Options);
        context.Database.EnsureCreated();

        return new TestDbContextScope<TestDbContext>(context);
    }
}
