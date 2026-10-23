namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Infrastructure.Database;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The <see cref="BlueprintQueryFilters"/> keys are reserved. Of two filters with the same key, the one
/// applied last wins, so an application filter under one of them would either replace the blueprint's
/// (for tenant isolation, a cross-tenant leak) or be dropped without a trace. Both paths fail instead.
/// </summary>
public sealed class ReservedQueryFilterKeyTests
{
    [Fact]
    public void ApplicationFilterUnderTheTenantKey_FailsTheModelBuild()
    {
        using var context = new ReservedTenantFilterDbContext(new DbContextOptionsBuilder<ReservedTenantFilterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var exception = Should.Throw<InvalidOperationException>(() => context.Model);

        exception.Message.ShouldContain(nameof(Booking));
        exception.Message.ShouldContain($"'{BlueprintQueryFilters.Tenant}'");
    }

    [Fact]
    public void ApplicationFilterUnderTheTenantKey_FailsADirectCall()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Booking>().HasQueryFilter(BlueprintQueryFilters.Tenant, b => b.Reference != string.Empty);

        Should.Throw<InvalidOperationException>(
            () => modelBuilder.ConfigureBlueprintMultiTenancy<TestTenant, TestDbContext>(scope.Context, addTenantForeignKey: false));
    }

    [Fact]
    public void ApplicationFilterUnderTheSoftDeleteKey_FailsADirectCall()
    {
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Booking>().HasQueryFilter(BlueprintQueryFilters.SoftDelete, b => b.Reference != string.Empty);

        var exception = Should.Throw<InvalidOperationException>(() => modelBuilder.ConfigureSoftDeleteFiltering());

        exception.Message.ShouldContain($"'{BlueprintQueryFilters.SoftDelete}'");
    }

    [Fact]
    public void ApplyingTheSameConfigurationTwice_IsAllowed()
    {
        using var scope = TestDbContextFactory.CreateInMemoryDbContext();
        var modelBuilder = new ModelBuilder();
        modelBuilder.Entity<Booking>();

        modelBuilder.ConfigureSoftDeleteFiltering().ConfigureSoftDeleteFiltering();
        modelBuilder.ConfigureBlueprintMultiTenancy<TestTenant, TestDbContext>(scope.Context, addTenantForeignKey: false);
        modelBuilder.ConfigureBlueprintMultiTenancy<TestTenant, TestDbContext>(scope.Context, addTenantForeignKey: false);

        modelBuilder.Model.FindEntityType(typeof(Booking))!.GetDeclaredQueryFilters().Count.ShouldBe(2);
    }
}
