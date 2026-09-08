namespace Csag.Blueprint.IntegrationTests.Tenancy;

using System.Net;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service-level integration tests for the tenant lifecycle against the real SQL container.
/// The host registers its context factory with <c>EnableRetryOnFailure</c>, so a retrying execution
/// strategy is active: <see cref="ITenantManager{TUser, TTenant}"/> must wrap its user-initiated
/// transactions in that strategy or EF Core rejects <c>BeginTransactionAsync</c> outright (the
/// BLUEAPI-87 regression class). These tests pin that wiring plus the transactional atomicity and
/// the destructive sync-members contract, and verify tenant-wide session revocation stays scoped
/// to the affected tenant. Tenant-session tests operate only on freshly created tenants and users,
/// because revoking a fixture client's tenant would drop its cached ticket for the rest of the run.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class TenantManagerTests(AppFixture app) : IntegrationTestBase(app)
{
    /// <summary>
    /// Authenticated-only endpoint without a permission policy, used to probe whether a client's
    /// session still authenticates (200) or has been revoked (401).
    /// </summary>
    private static readonly Uri AuthProbeUri = new("/api/maintenance-records", UriKind.Relative);

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

    [Fact]
    public async Task RevokeTenantSessionsRevokesOnlySessionsScopedToThatTenantAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a fresh tenant with a fresh member signed in, so their session is scoped to
        // it, plus a control user whose session is scoped to tenant A.
        var doomedTenantId = await this.SeedTenantWithMembersAsync("Doomed Tenant");
        var doomedUser = await this.CreateTestUserAsync("doomed_member@test.local", doomedTenantId);
        var controlUser = await this.CreateTestUserAsync("control_member@test.local", SeedData.TenantAId);

        using var doomedClient = await this.SignInAsync(doomedUser.Email!);
        using var controlClient = await this.SignInAsync(controlUser.Email!);

        var doomedBefore = await doomedClient.GetAsync(AuthProbeUri, ct);
        await doomedBefore.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);

        // Act — revoke every session scoped to the doomed tenant (what a tenant delete triggers so
        // no cached ticket keeps authorizing requests against a tenant that no longer exists).
        int revokedCount;
        using (var serviceScope = this.App.Services.CreateScope())
        {
            var sessionManager = serviceScope.ServiceProvider.GetRequiredService<ISessionManager>();
            revokedCount = await sessionManager.RevokeTenantSessionsAsync(doomedTenantId, ct);
        }

        // Assert — exactly the doomed tenant's session was revoked: its cached ticket is gone
        // (immediate 401) and its tracking row removed, while the control session keeps working.
        revokedCount.ShouldBe(1);

        var doomedAfter = await doomedClient.GetAsync(AuthProbeUri, ct);
        await doomedAfter.ShouldHaveStatusCodeAsync(HttpStatusCode.Unauthorized, "a session scoped to the revoked tenant must be signed out immediately");

        var controlAfter = await controlClient.GetAsync(AuthProbeUri, ct);
        await controlAfter.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, "sessions scoped to other tenants must survive");

        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == doomedUser.Id, ct)).ShouldBeFalse(
            "the revoked tenant's session tracking rows must be removed");
        (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == controlUser.Id, ct)).ShouldBeTrue();
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

    /// <summary>
    /// Creates a fresh user with the shared default password and makes it a member of the given
    /// tenant, so a plain sign-in lands in that tenant.
    /// </summary>
    /// <param name="email">The new user's email (also used as user name).</param>
    /// <param name="tenantId">The tenant to create the membership in.</param>
    /// <returns>The created user.</returns>
    private async Task<TestUser> CreateTestUserAsync(string email, Guid tenantId)
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = this.App.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();

        var user = new TestUser { Email = email, UserName = email };
        var createResult = await userManager.CreateAsync(user, SeedData.DefaultPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        context.TenantMemberships.Add(new BlueprintTenantMembership<TestUser, TestTenant>
        {
            UserId = user.Id,
            TenantId = tenantId,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(ct);

        return user;
    }

    /// <summary>
    /// Signs the given user in on a brand-new client (its own cookie container), tracking an
    /// active session scoped to the user's only tenant membership.
    /// </summary>
    /// <param name="email">The user's email; the password is the shared <see cref="SeedData.DefaultPassword"/>.</param>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = this.App.CreateClient();
        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = email,
                Password = SeedData.DefaultPassword,
            });

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, $"failed to authenticate test user '{email}'");
        AppFixture.SetCsrfHeaderFromResponse(client, rsp);
        return client;
    }
}
