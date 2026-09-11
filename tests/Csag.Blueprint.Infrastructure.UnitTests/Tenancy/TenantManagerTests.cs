namespace Csag.Blueprint.Infrastructure.UnitTests.Tenancy;

using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Tenancy;
using Csag.Blueprint.Testing.Unit;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Unit tests for the membership join stamps in <see cref="TenantManager{TUser, TTenant, TContext}"/>. Both
/// write paths read the injected clock, and each test advances the fake clock off its starting instant first,
/// so the expected stamp is a value a regression to the wall clock could not produce.
/// </summary>
public sealed class TenantManagerTests : IDisposable
{
    private readonly TestDbContextScope<TestDbContext> scope;
    private readonly TenantManager<TestUser, TestTenant, TestDbContext> manager;
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 5, 2, 11, 30, 0, TimeSpan.Zero));

    public TenantManagerTests()
    {
        this.scope = TestDbContextFactory.CreateInMemoryDbContext();
        this.manager = new TenantManager<TestUser, TestTenant, TestDbContext>(this.scope.Context, this.clock);
    }

    public void Dispose()
    {
        this.scope.Dispose();
    }

    [Fact]
    public async Task AddMemberAsync_StampsJoinedAtFromTheClock()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        this.clock.Advance(TimeSpan.FromHours(5));
        var joinedAt = this.clock.GetUtcNow();

        // Act
        var result = await this.manager.AddMemberAsync(userId, TestDbContextFactory.TestTenantId, ct);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var membership = await this.Memberships().SingleAsync(m => m.UserId == userId, ct);
        membership.JoinedAt.ShouldBe(joinedAt);
    }

    [Fact]
    public async Task SyncMembersAsync_StampsJoinedAtOnNewMembersFromTheClock()
    {
        // Arrange — one member already joined, so the sync both keeps an existing row and adds a new one. The
        // clock moves in between, which is what shows the added row is stamped at the sync instant and the
        // kept row is left at the instant it originally joined.
        var ct = TestContext.Current.CancellationToken;
        var existingUserId = Guid.NewGuid();
        var addedUserId = Guid.NewGuid();
        await this.manager.AddMemberAsync(existingUserId, TestDbContextFactory.TestTenantId, ct);
        var originalJoinedAt = this.clock.GetUtcNow();

        this.clock.Advance(TimeSpan.FromDays(2));
        var syncedAt = this.clock.GetUtcNow();

        // Act
        var result = await this.manager.SyncMembersAsync(
            TestDbContextFactory.TestTenantId, [existingUserId, addedUserId], ct);

        // Assert
        result.Succeeded.ShouldBeTrue();
        (await this.Memberships().SingleAsync(m => m.UserId == addedUserId, ct)).JoinedAt.ShouldBe(syncedAt);
        (await this.Memberships().SingleAsync(m => m.UserId == existingUserId, ct)).JoinedAt.ShouldBe(originalJoinedAt);
    }

    private IQueryable<BlueprintTenantMembership<TestUser, TestTenant>> Memberships()
    {
        return this.scope.Context.Set<BlueprintTenantMembership<TestUser, TestTenant>>().AsNoTracking();
    }
}
