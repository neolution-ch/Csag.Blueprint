namespace Csag.Blueprint.Infrastructure.UnitTests.Conventions;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Contracts;
using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Infrastructure.UnitTests.TestModel;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The tenant and soft-delete filters are registered under separate names so a query can drop soft
/// deletion without losing tenant isolation. Runs through <c>BlueprintDbContext</c>, on an entity type
/// registered after <c>base.OnModelCreating</c>. The in-memory provider evaluates the same named
/// filters; the blueprint configurations use SQL Server column types that SQLite cannot create.
/// </summary>
public sealed class TenantScopedQueryFilterTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002", System.Globalization.CultureInfo.InvariantCulture);

    private readonly TenantScopedDbContext context;

    public TenantScopedQueryFilterTests()
    {
        this.context = new TenantScopedDbContext(
            new DbContextOptionsBuilder<TenantScopedDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        this.context.Tenants.AddRange(
            new TestTenant { Id = TenantA, Name = "A" },
            new TestTenant { Id = TenantB, Name = "B" });
        this.context.Set<Booking>().AddRange(
            new Booking { TenantId = TenantA, Reference = "a-live" },
            new Booking { TenantId = TenantA, Reference = "a-deleted", DeletedAt = DateTimeOffset.UnixEpoch },
            new Booking { TenantId = TenantB, Reference = "b-live" },
            new Booking { TenantId = TenantB, Reference = "b-deleted", DeletedAt = DateTimeOffset.UnixEpoch });
        this.context.SaveChanges();
        this.context.ChangeTracker.Clear();

        TenantContext.SetTenant(TenantA);
    }

    [Fact]
    public async Task DefaultQuery_AppliesBothFilters()
    {
        (await ReferencesAsync(this.context.Set<Booking>())).ShouldBe(["a-live"]);
    }

    [Fact]
    public async Task IgnoreSoftDeleteFilter_KeepsTenantIsolation()
    {
        (await ReferencesAsync(this.context.Set<Booking>().IgnoreSoftDeleteFilter())).ShouldBe(["a-deleted", "a-live"]);
    }

    [Fact]
    public async Task OnlySoftDeleted_KeepsTenantIsolation()
    {
        (await ReferencesAsync(this.context.Set<Booking>().OnlySoftDeleted())).ShouldBe(["a-deleted"]);
    }

    [Fact]
    public async Task TenantFilter_FollowsTheAmbientTenantPerQuery()
    {
        TenantContext.SetTenant(TenantB);

        (await ReferencesAsync(this.context.Set<Booking>())).ShouldBe(["b-live"]);
    }

    [Fact]
    public void TenantNavigationLeftToConvention_SharesTheSingleTenantForeignKey()
    {
        var memo = this.context.Model.FindEntityType(typeof(Memo))!;

        var foreignKey = memo.GetForeignKeys().ShouldHaveSingleItem();
        foreignKey.Properties.ShouldHaveSingleItem().Name.ShouldBe(nameof(IMustHaveTenant.TenantId));
        foreignKey.DependentToPrincipal!.Name.ShouldBe(nameof(Memo.Tenant));
        foreignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
    }

    public void Dispose()
    {
        TenantContext.Clear();
        this.context.Dispose();
    }

    private static async Task<string[]> ReferencesAsync(IQueryable<Booking> query)
        => [.. (await query.Select(b => b.Reference).ToListAsync(TestContext.Current.CancellationToken)).Order(StringComparer.Ordinal)];
}
