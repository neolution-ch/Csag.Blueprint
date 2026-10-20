namespace Csag.Blueprint.IntegrationTests.Tenancy;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service-level integration tests for the tenant lifecycle against the real SQL container.
/// The host registers its context factory with <c>EnableRetryOnFailure</c>, so a retrying execution
/// strategy is active: <see cref="ITenantManager{TUser, TTenant}"/> must wrap its user-initiated
/// transactions in that strategy or EF Core rejects <c>BeginTransactionAsync</c> outright (the
/// BLUEAPI-87 regression class). These tests pin that wiring plus the transactional atomicity and
/// the destructive sync-members contract.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TenantManagerTests(AppFixture app) : IntegrationTestBase(app)
{
    [Fact]
    public async Task CreateTenantSucceedsUnderRetryingStrategyAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        // Act — create a tenant with an initial two-user roster through the real service, real
        // transaction, and real retrying execution strategy.
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var tenantManager = serviceScope.ServiceProvider.GetRequiredService<ITenantManager<TestUser, TestTenant>>();
            var result = await tenantManager.CreateTenantAsync(
                new TestTenant { Id = tenantId, Name = "Fresh Tenant" },
                [SeedData.ManagerAUserId, SeedData.ViewerAUserId],
                ct);

            result.Succeeded.ShouldBeTrue(result.ErrorMessage);
        }

        // Assert — the tenant row and both membership rows committed together, exactly once.
        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.Tenants.CountAsync(t => t.Id == tenantId, ct)).ShouldBe(1);

        var memberIds = await scope.Context.TenantMemberships
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        memberIds.Count.ShouldBe(2);
        memberIds.ShouldContain(SeedData.ManagerAUserId);
        memberIds.ShouldContain(SeedData.ViewerAUserId);
    }

    [Fact]
    public async Task CreateTenantFailingMidWorkflowLeavesNoOrphanRowsAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();

        // Act — the tenant insert succeeds, then the membership insert violates the user foreign
        // key. The failure surfaces after the tenant row was already written inside the
        // transaction, so only a rollback can undo it.
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var tenantManager = serviceScope.ServiceProvider.GetRequiredService<ITenantManager<TestUser, TestTenant>>();

            await Should.ThrowAsync<DbUpdateException>(async () =>
                await tenantManager.CreateTenantAsync(
                    new TestTenant { Id = tenantId, Name = "Half-Created Tenant" },
                    [missingUserId],
                    ct));
        }

        // Assert — the whole workflow rolled back: no orphan tenant shell, no membership rows.
        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.Tenants.AnyAsync(t => t.Id == tenantId, ct)).ShouldBeFalse("a failed create must not leave an orphan tenant row");
        (await scope.Context.TenantMemberships.AnyAsync(m => m.TenantId == tenantId, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateTenantSyncsMembershipsToExactlyThePassedRosterAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a tenant with a two-user roster.
        var tenantId = await this.SeedTenantWithMembersAsync("Original", SeedData.ManagerAUserId, SeedData.ViewerAUserId);

        // Act — update the tenant, passing a roster that no longer contains ViewerA. The scoped
        // context resolved here is the same instance the tenant manager uses, mirroring how an
        // endpoint loads, mutates, and saves the entity within one request scope.
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();
            var tenantManager = serviceScope.ServiceProvider.GetRequiredService<ITenantManager<TestUser, TestTenant>>();

            var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId, ct);
            tenant.Name = "Renamed";

            var result = await tenantManager.UpdateTenantAsync(tenant, [SeedData.ManagerAUserId], ct);
            result.Succeeded.ShouldBeTrue(result.ErrorMessage);
        }

        // Assert — the rename persisted and the roster now contains exactly the passed users:
        // memberships absent from the list are REMOVED, not left alone. Callers must always pass
        // the full desired roster, never a delta.
        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.Tenants.SingleAsync(t => t.Id == tenantId, ct)).Name.ShouldBe("Renamed");

        var memberIds = await scope.Context.TenantMemberships
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        memberIds.ShouldHaveSingleItem().ShouldBe(SeedData.ManagerAUserId);
    }

    [Fact]
    public async Task UpdateTenantWithEmptyUserListWipesTheRosterAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a tenant with a two-user roster.
        var tenantId = await this.SeedTenantWithMembersAsync("Roster Wipe", SeedData.ManagerAUserId, SeedData.ViewerAUserId);

        // Act — update with an EMPTY roster.
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();
            var tenantManager = serviceScope.ServiceProvider.GetRequiredService<ITenantManager<TestUser, TestTenant>>();

            var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId, ct);
            var result = await tenantManager.UpdateTenantAsync(tenant, [], ct);
            result.Succeeded.ShouldBeTrue(result.ErrorMessage);
        }

        // Assert — the destructive edge of the sync contract: an empty list removes every
        // membership while the tenant itself survives.
        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.TenantMemberships.AnyAsync(m => m.TenantId == tenantId, ct)).ShouldBeFalse(
            "sync-to-empty must wipe the roster — this is the documented, deliberately destructive contract");
        (await scope.Context.Tenants.AnyAsync(t => t.Id == tenantId, ct)).ShouldBeTrue();
    }

    /// <summary>
    /// Seeds a tenant with the given members directly through the fixture's database scope,
    /// bypassing the tenant manager so its behavior stays the subject under test.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    /// <param name="memberUserIds">Users to create memberships for.</param>
    /// <returns>The id of the seeded tenant.</returns>
    private async Task<Guid> SeedTenantWithMembersAsync(string name, params Guid[] memberUserIds)
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.CreateDbContextScope();
        var tenant = new TestTenant { Id = Guid.NewGuid(), Name = name };
        scope.Context.Tenants.Add(tenant);

        foreach (var userId in memberUserIds)
        {
            scope.Context.TenantMemberships.Add(new BlueprintTenantMembership<TestUser, TestTenant>
            {
                UserId = userId,
                TenantId = tenant.Id,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }

        await scope.Context.SaveChangesAsync(ct);
        return tenant.Id;
    }
}
