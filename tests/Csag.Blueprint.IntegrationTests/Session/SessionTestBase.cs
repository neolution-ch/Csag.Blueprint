namespace Csag.Blueprint.IntegrationTests.Session;

using System.Net;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.TestHost.Session;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared arrangements for the session integration tests. Every test operates on freshly created users (and, where
/// it ends a tenant's sessions, a freshly created tenant), so revocation never touches the fixture's
/// pre-authenticated clients: their tracked rows are restored by the per-test snapshot, but their cached tickets live
/// in the host's memory cache and would be gone for the rest of the run.
/// </summary>
/// <param name="app">The shared application fixture.</param>
public abstract class SessionTestBase(AppFixture app) : IntegrationTestBase(app)
{
    /// <summary>
    /// Authenticated-only endpoint without a permission policy, used to probe whether a client's session still
    /// authenticates (200) or has been revoked (401).
    /// </summary>
    protected static readonly Uri AuthProbeUri = new("/api/maintenance-records", UriKind.Relative);

    /// <summary>
    /// Asserts whether a client's session still authenticates.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="expected">200 for a live session, 401 for an ended one.</param>
    /// <param name="because">Why the status is expected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected static async Task ShouldAuthenticateAsync(HttpClient client, HttpStatusCode expected, string? because = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var rsp = await client.GetAsync(AuthProbeUri, ct);
        await rsp.ShouldHaveStatusCodeAsync(expected, because, ct);
    }

    /// <summary>
    /// Creates a fresh user with the shared default password and makes it a member of the given tenants (in the given
    /// order, so the first tenant becomes the sign-in default).
    /// </summary>
    /// <param name="email">The new user's email (also used as user name).</param>
    /// <param name="tenantIds">Tenants to create memberships for.</param>
    /// <returns>The created user.</returns>
    protected async Task<TestUser> CreateTestUserAsync(string email, params Guid[] tenantIds)
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
        foreach (var tenantId in tenantIds)
        {
            context.TenantMemberships.Add(new BlueprintTenantMembership<TestUser, TestTenant>
            {
                UserId = user.Id,
                TenantId = tenantId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
        }

        await context.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Seeds a tenant without members.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    /// <returns>The id of the seeded tenant.</returns>
    protected async Task<Guid> SeedTenantAsync(string name)
    {
        using var scope = this.App.CreateDbContextScope();
        var tenant = new TestTenant { Id = Guid.NewGuid(), Name = name };
        scope.Context.Tenants.Add(tenant);
        await scope.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return tenant.Id;
    }

    /// <summary>
    /// Signs the given user in on a brand-new client (its own cookie container), tracking an active session scoped
    /// to the resolved tenant.
    /// </summary>
    /// <param name="email">The user's email; the password is the shared <see cref="SeedData.DefaultPassword"/>.</param>
    /// <param name="tenantId">Tenant to scope the session to; defaults to the user's first membership.</param>
    /// <returns>An authenticated client.</returns>
    protected async Task<HttpClient> SignInAsync(string email, Guid? tenantId = null)
    {
        var client = this.App.CreateClient();
        await this.SignInWithAsync(client, email, tenantId);
        return client;
    }

    /// <summary>
    /// Signs the given user in on an existing client, which keeps sending whatever session cookie it already holds.
    /// </summary>
    /// <param name="client">The client to sign in with.</param>
    /// <param name="email">The user's email; the password is the shared <see cref="SeedData.DefaultPassword"/>.</param>
    /// <param name="tenantId">Tenant to scope the session to; defaults to the user's first membership.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task SignInWithAsync(HttpClient client, string email, Guid? tenantId = null)
    {
        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest
            {
                Email = email,
                Password = SeedData.DefaultPassword,
                TenantId = tenantId,
            });

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, $"failed to authenticate test user '{email}'");
        AppFixture.SetCsrfHeaderFromResponse(client, rsp);
    }

    /// <summary>
    /// Reads the single tracked session of a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The user's only tracking row.</returns>
    protected async Task<BlueprintActiveSession> GetSingleSessionAsync(Guid userId)
    {
        using var scope = this.App.CreateDbContextScope();
        return await scope.Context.ActiveSessions
            .AsNoTracking()
            .SingleAsync(s => s.UserId == userId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Runs an intent of the session lifecycle outside any request, as a background job or a test would.
    /// </summary>
    /// <param name="intent">The intent to run.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task RunIntentAsync(Func<ISessionLifecycle, Task> intent)
    {
        using var scope = this.App.Services.CreateScope();
        await intent(scope.ServiceProvider.GetRequiredService<ISessionLifecycle>());
    }

    /// <summary>
    /// Gets the revocations the host's recording participant was told about for a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The revocations, oldest first.</returns>
    protected List<SessionRevocation> RevocationsFor(Guid userId)
    {
        return this.App.Services.GetRequiredService<RecordingRevocationParticipant>().Revocations
            .Where(r => r.Subject == SessionSubject.User(userId))
            .ToList();
    }
}
