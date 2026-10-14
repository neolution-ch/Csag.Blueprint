namespace Csag.Blueprint.IntegrationTests.Session;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.ChangePassword;
using Csag.Blueprint.TestHost.Endpoints.Auth.SwitchTenant;
using Csag.Blueprint.Testing.Extensions;
using Csag.Blueprint.Tests.Shared.Entities;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for each <see cref="ISessionLifecycle"/> intent against the real SQL container and the
/// TestHost's endpoints: which sessions end, which keep running with rebuilt claims, and which revocations reach the
/// application's participants.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class SessionLifecycleTests(AppFixture app) : SessionTestBase(app)
{
    private static readonly Uri SessionsUri = new("/api/auth/sessions", UriKind.Relative);

    [Fact]
    public async Task CredentialsChanged_FromTheAccountsOwnSession_KeepsThatSessionAndEndsTheOthersAsync()
    {
        // Arrange — one account signed in on three devices.
        var user = await this.CreateTestUserAsync("credentials_changed_test@test.local", SeedData.TenantAId);
        using var changingClient = await this.SignInAsync(user.Email!);
        using var otherClient1 = await this.SignInAsync(user.Email!);
        using var otherClient2 = await this.SignInAsync(user.Email!);

        // Act — the password change reports the credential change once committed.
        var (rsp, _) = await changingClient.POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(
            new ChangePasswordRequest { CurrentPassword = SeedData.DefaultPassword, NewPassword = "Changed@456" });
        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.NoContent, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await ShouldAuthenticateAsync(changingClient, HttpStatusCode.OK, "the session that changed the password stays signed in");
        await ShouldAuthenticateAsync(otherClient1, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(otherClient2, HttpStatusCode.Unauthorized);

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.CountAsync(s => s.UserId == user.Id, TestContext.Current.CancellationToken)).ShouldBe(1);
        }

        var revocation = this.RevocationsFor(user.Id).ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.CredentialsChanged);
    }

    [Fact]
    public async Task CredentialsChanged_WithoutACallerSession_EndsEverySessionAsync()
    {
        // A reset from an emailed link, or a background job, has no session to keep.
        var user = await this.CreateTestUserAsync("credentials_reset_test@test.local", SeedData.TenantAId);
        using var client1 = await this.SignInAsync(user.Email!);
        using var client2 = await this.SignInAsync(user.Email!);

        await this.RunIntentAsync(lifecycle => lifecycle.CredentialsChangedAsync(user.Id));

        await ShouldAuthenticateAsync(client1, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(client2, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessRevoked_EndsEverySessionOfTheAccountAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — one user signed in on two devices, in two tenants.
        var user = await this.CreateTestUserAsync("access_revoked_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var client1 = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        using var client2 = await this.SignInAsync(user.Email!, SeedData.TenantBId);

        // Act
        await this.RunIntentAsync(lifecycle => lifecycle.AccessRevokedAsync(user.Id));

        // Assert — both tracking rows are gone and both cookies stopped authenticating, proving the cached tickets
        // were dropped alongside the rows.
        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == user.Id, ct)).ShouldBeFalse();
        }

        await ShouldAuthenticateAsync(client1, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(client2, HttpStatusCode.Unauthorized);

        var revocation = this.RevocationsFor(user.Id).ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.AccessRevoked);
        revocation.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task AccessRevokedInATenant_LeavesTheAccountsSessionsInOtherTenantsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — a shared account signed in to two tenants at once.
        var user = await this.CreateTestUserAsync("access_revoked_tenant_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var tenantAClient = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        using var tenantBClient = await this.SignInAsync(user.Email!, SeedData.TenantBId);

        // Act
        await this.RunIntentAsync(lifecycle => lifecycle.AccessRevokedAsync(user.Id, SeedData.TenantAId));

        // Assert — exactly the tenant-A session ended.
        using (var scope = this.App.CreateDbContextScope())
        {
            var remaining = await scope.Context.ActiveSessions.Where(s => s.UserId == user.Id).ToListAsync(ct);
            remaining.ShouldHaveSingleItem().CurrentTenantId.ShouldBe(SeedData.TenantBId);
        }

        await ShouldAuthenticateAsync(tenantAClient, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(tenantBClient, HttpStatusCode.OK, "sessions scoped to other tenants must survive a tenant-scoped revocation");
        this.RevocationsFor(user.Id).ShouldHaveSingleItem().TenantId.ShouldBe(SeedData.TenantAId);
    }

    [Fact]
    public async Task ClaimsChanged_RebuildsTheCachedClaimsOfEverySessionAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the account changes its preferred language after signing in on two devices.
        var user = await this.CreateTestUserAsync("claims_changed_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var tenantAClient = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        using var tenantBClient = await this.SignInAsync(user.Email!, SeedData.TenantBId);
        await this.SetPreferredLanguageAsync(user.Id, "de");

        // Act
        await this.RunIntentAsync(lifecycle => lifecycle.ClaimsChangedAsync(user.Id));

        // Assert — both tickets carry the new language and keep their own tenant.
        foreach (var (tenantId, language) in await this.ReadTicketClaimsAsync(user.Id, ct))
        {
            language.ShouldBe("de");
            tenantId.ShouldNotBeNull();
        }

        await ShouldAuthenticateAsync(tenantAClient, HttpStatusCode.OK);
        await ShouldAuthenticateAsync(tenantBClient, HttpStatusCode.OK);
        this.RevocationsFor(user.Id).ShouldBeEmpty();
    }

    [Fact]
    public async Task ClaimsChangedInATenant_RebuildsOnlyThatTenantsSessionsAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = await this.CreateTestUserAsync("claims_changed_tenant_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var tenantAClient = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        using var tenantBClient = await this.SignInAsync(user.Email!, SeedData.TenantBId);
        await this.SetPreferredLanguageAsync(user.Id, "de");

        await this.RunIntentAsync(lifecycle => lifecycle.ClaimsChangedAsync(user.Id, SeedData.TenantAId));

        var claims = await this.ReadTicketClaimsAsync(user.Id, ct);
        claims.Single(c => c.TenantId == SeedData.TenantAId.ToString()).Language.ShouldBe("de");
        claims.Single(c => c.TenantId == SeedData.TenantBId.ToString()).Language.ShouldBeNull();
    }

    [Fact]
    public async Task TenantRemoved_EndsTheTenantsSessionsOfEveryAccountOnlyAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — two accounts in a fresh tenant (a fixture tenant would take the shared clients with it), one of
        // them also signed in to tenant A, plus a control account in tenant A.
        var doomedTenantId = await this.SeedTenantAsync("Doomed Tenant");
        var member = await this.CreateTestUserAsync("tenant_removed_member@test.local", doomedTenantId, SeedData.TenantAId);
        var otherMember = await this.CreateTestUserAsync("tenant_removed_other@test.local", doomedTenantId);
        var control = await this.CreateTestUserAsync("tenant_removed_control@test.local", SeedData.TenantAId);
        using var memberInDoomed = await this.SignInAsync(member.Email!, doomedTenantId);
        using var memberInTenantA = await this.SignInAsync(member.Email!, SeedData.TenantAId);
        using var otherMemberInDoomed = await this.SignInAsync(otherMember.Email!);
        using var controlClient = await this.SignInAsync(control.Email!);

        // Act
        await this.RunIntentAsync(lifecycle => lifecycle.TenantRemovedAsync(doomedTenantId));

        // Assert
        await ShouldAuthenticateAsync(memberInDoomed, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(otherMemberInDoomed, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(memberInTenantA, HttpStatusCode.OK, "the account's session in another tenant must survive");
        await ShouldAuthenticateAsync(controlClient, HttpStatusCode.OK);

        using (var scope = this.App.CreateDbContextScope())
        {
            (await scope.Context.ActiveSessions.AnyAsync(s => s.CurrentTenantId == doomedTenantId, ct)).ShouldBeFalse();
        }

        var revocation = this.App.Services.GetRequiredService<TestHost.Session.RecordingRevocationParticipant>().Revocations
            .Single(r => r.TenantId == doomedTenantId);
        revocation.Reason.ShouldBe(SessionRevocationReason.TenantRemoved);
        revocation.Subject.ShouldBeNull();
    }

    [Fact]
    public async Task SwitchTenant_MovesOnlyTheCallersSessionAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the same account on two devices, both in tenant A; the second device signs in last, so a switch
        // that picked the newest session rather than the caller's would move the wrong one.
        var user = await this.CreateTestUserAsync("switch_tenant_test@test.local", SeedData.TenantAId, SeedData.TenantBId);
        using var caller = await this.SignInAsync(user.Email!, SeedData.TenantAId);
        var callerKey = (await this.GetSingleSessionAsync(user.Id)).SessionKey;
        using var otherDevice = await this.SignInAsync(user.Email!, SeedData.TenantAId);

        // Act
        var outcome = await SwitchTenantAsync(caller, SeedData.TenantBId);

        // Assert — the caller's row and ticket moved to tenant B; the other device stayed in tenant A.
        outcome.ShouldBe(SessionTenantSwitchOutcome.Switched);
        using (var scope = this.App.CreateDbContextScope())
        {
            var sessions = await scope.Context.ActiveSessions.AsNoTracking().Where(s => s.UserId == user.Id).ToListAsync(ct);
            sessions.Single(s => s.SessionKey == callerKey).CurrentTenantId.ShouldBe(SeedData.TenantBId);
            sessions.Single(s => s.SessionKey != callerKey).CurrentTenantId.ShouldBe(SeedData.TenantAId);
        }

        var ticket = await this.App.Services.GetRequiredService<ITicketCacheService>().GetTicketAsync(callerKey, ct);
        ticket.ShouldNotBeNull().Principal.FindFirstValue(IdentityClaimTypes.TenantId).ShouldBe(SeedData.TenantBId.ToString());
        await ShouldAuthenticateAsync(caller, HttpStatusCode.OK);
        await ShouldAuthenticateAsync(otherDevice, HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwitchTenant_ToATenantTheAccountIsNotAMemberOf_LeavesTheSessionAsync()
    {
        var user = await this.CreateTestUserAsync("switch_tenant_non_member_test@test.local", SeedData.TenantAId);
        using var caller = await this.SignInAsync(user.Email!);

        var outcome = await SwitchTenantAsync(caller, SeedData.TenantBId);

        outcome.ShouldBe(SessionTenantSwitchOutcome.NotAMember);
        (await this.GetSingleSessionAsync(user.Id)).CurrentTenantId.ShouldBe(SeedData.TenantAId);
    }

    [Fact]
    public async Task SwitchTenant_OutsideARequest_ReportsNoActiveSessionAsync()
    {
        var outcome = SessionTenantSwitchOutcome.Switched;

        await this.RunIntentAsync(async lifecycle => outcome = await lifecycle.SwitchTenantAsync(SeedData.TenantBId));

        outcome.ShouldBe(SessionTenantSwitchOutcome.NoActiveSession);
    }

    [Fact]
    public async Task ListMySessions_FlagsTheCallersSessionAndNeverCarriesAKeyAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange
        var user = await this.CreateTestUserAsync("list_sessions_test@test.local", SeedData.TenantAId);
        using var caller = await this.SignInAsync(user.Email!);
        var callerRow = await this.GetSingleSessionAsync(user.Id);
        using var otherDevice = await this.SignInAsync(user.Email!);

        // Act
        var rsp = await caller.GetAsync(SessionsUri, ct);
        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK, cancellationToken: ct);
        var body = await rsp.Content.ReadAsStringAsync(ct);
        var sessions = await rsp.Content.ReadFromJsonAsync<List<ActiveSessionInfo>>(ct);

        // Assert
        sessions.ShouldNotBeNull().Count.ShouldBe(2);
        sessions.Single(s => s.IsCurrent).Id.ShouldBe(callerRow.Id);
        body.ShouldNotContain(callerRow.SessionKey);
        body.ShouldNotContain("sessionKey", Case.Insensitive);
    }

    [Fact]
    public async Task RevokeMySession_AnotherOfTheCallersSessions_EndsItAndKeepsTheCallerAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = await this.CreateTestUserAsync("revoke_my_other_test@test.local", SeedData.TenantAId);
        using var otherDevice = await this.SignInAsync(user.Email!);
        var otherRow = await this.GetSingleSessionAsync(user.Id);
        using var caller = await this.SignInAsync(user.Email!);

        var rsp = await caller.DeleteAsync(new Uri($"{SessionsUri}/{otherRow.Id}", UriKind.Relative), ct);

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.NoContent, cancellationToken: ct);
        await ShouldAuthenticateAsync(otherDevice, HttpStatusCode.Unauthorized);
        await ShouldAuthenticateAsync(caller, HttpStatusCode.OK);
        var revocation = this.RevocationsFor(user.Id).ShouldHaveSingleItem();
        revocation.Reason.ShouldBe(SessionRevocationReason.SessionRevokedByOwner);
        revocation.SessionId.ShouldBe(otherRow.Id);
    }

    [Fact]
    public async Task RevokeMySession_TheCallersOwnSession_SignsTheCallerOutAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = await this.CreateTestUserAsync("revoke_my_current_test@test.local", SeedData.TenantAId);
        using var caller = await this.SignInAsync(user.Email!);
        var callerRow = await this.GetSingleSessionAsync(user.Id);

        var rsp = await caller.DeleteAsync(new Uri($"{SessionsUri}/{callerRow.Id}", UriKind.Relative), ct);

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.NoContent, cancellationToken: ct);
        rsp.Headers.TryGetValues("Set-Cookie", out var setCookies).ShouldBeTrue("signing out clears the session cookie");
        setCookies.ShouldContain(c => c.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        await ShouldAuthenticateAsync(caller, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeMySession_AnotherAccountsSession_LeavesItAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        var owner = await this.CreateTestUserAsync("revoke_foreign_owner_test@test.local", SeedData.TenantAId);
        var stranger = await this.CreateTestUserAsync("revoke_foreign_stranger_test@test.local", SeedData.TenantAId);
        using var ownerClient = await this.SignInAsync(owner.Email!);
        var ownerRow = await this.GetSingleSessionAsync(owner.Id);
        using var strangerClient = await this.SignInAsync(stranger.Email!);

        var rsp = await strangerClient.DeleteAsync(new Uri($"{SessionsUri}/{ownerRow.Id}", UriKind.Relative), ct);

        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.NoContent, cancellationToken: ct);
        await ShouldAuthenticateAsync(ownerClient, HttpStatusCode.OK, "a session can only be ended by its own account");
        this.RevocationsFor(owner.Id).ShouldBeEmpty();
        this.RevocationsFor(stranger.Id).ShouldBeEmpty();
    }

    private static async Task<SessionTenantSwitchOutcome> SwitchTenantAsync(HttpClient client, Guid tenantId)
    {
        var (rsp, result) = await client.POSTAsync<SwitchTenantEndpoint, SwitchTenantRequest, SwitchTenantResponse>(
            new SwitchTenantRequest { TenantId = tenantId });
        await rsp.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);
        return result.Outcome;
    }

    private async Task SetPreferredLanguageAsync(Guid userId, string language)
    {
        using var scope = this.App.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        user.ShouldNotBeNull().PreferredLanguage = language;
        (await userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
    }

    private async Task<List<(string? TenantId, string? Language)>> ReadTicketClaimsAsync(Guid userId, CancellationToken ct)
    {
        List<string> sessionKeys;
        using (var scope = this.App.CreateDbContextScope())
        {
            sessionKeys = await scope.Context.ActiveSessions.Where(s => s.UserId == userId).Select(s => s.SessionKey).ToListAsync(ct);
        }

        var ticketCache = this.App.Services.GetRequiredService<ITicketCacheService>();
        var claims = new List<(string? TenantId, string? Language)>();
        foreach (var sessionKey in sessionKeys)
        {
            var principal = (await ticketCache.GetTicketAsync(sessionKey, ct)).ShouldNotBeNull().Principal;
            claims.Add((principal.FindFirstValue(IdentityClaimTypes.TenantId), principal.FindFirstValue(IdentityClaimTypes.PreferredLanguage)));
        }

        return claims;
    }
}
