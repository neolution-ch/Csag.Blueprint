namespace Csag.Blueprint.IntegrationTests.Session;

using System.Net;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.TestHost;
using Csag.Blueprint.TestHost.Endpoints.Auth.Login;
using Csag.Blueprint.TestHost.Time;
using Csag.Blueprint.Tests.Shared.Entities;
using Csag.Blueprint.Web.Options.Api.Security;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Integration tests for the session cookie the package configures, through real requests against the TestHost: the
/// lifetime comes from <c>SessionExpirationHours</c> alone, Identity's security-stamp validation never signs a
/// session out, sliding renewal keeps the row in step, and the host's eligibility rule is asked at sign-in and at
/// renewal. The time-dependent tests pin the host clock, which the cookie handler reads, rather than waiting.
/// </summary>
[Collection(nameof(AppFixtureCollection))]
public sealed class CookieSessionTests(AppFixture app) : SessionTestBase(app)
{
    // Slack for the elapsed time between capturing "now" and the sign-in stamping the ticket. Far smaller than any
    // real drift (a doubled lifetime would miss by days), so it cannot mask a bug.
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(30);

    [Fact]
    public void SessionCookie_TakesItsLifetimeFromSessionExpirationHoursAndSlides()
    {
        var securitySettings = this.App.Services.GetRequiredService<IOptions<SecuritySettings>>().Value;
        var options = this.App.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);

        options.ExpireTimeSpan.ShouldBe(TimeSpan.FromHours(securitySettings.SessionExpirationHours));
        options.SlidingExpiration.ShouldBeTrue();
        options.Cookie.HttpOnly.ShouldBeTrue();
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Lax, "the host's callback chose Lax for the external sign-in's cross-site return");
    }

    [Fact]
    public async Task SignIn_WithoutAnExpiry_StoresATicketThatExpiresAfterTheConfiguredLifetimeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the TestHost's login endpoint states neither an expiry nor persistence.
        var lifetime = TimeSpan.FromHours(this.App.Services.GetRequiredService<IOptions<SecuritySettings>>().Value.SessionExpirationHours);
        var user = await this.CreateTestUserAsync("lifetime_test@test.local", SeedData.TenantAId);
        var client = this.App.CreateClient();

        // Act
        var before = DateTimeOffset.UtcNow;
        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest { Email = user.Email!, Password = SeedData.DefaultPassword });
        var after = DateTimeOffset.UtcNow;
        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert — the ticket, the row and the persistent cookie all expire after the configured lifetime.
        var session = await this.GetSingleSessionAsync(user.Id);
        var ticket = (await this.App.Services.GetRequiredService<ITicketCacheService>().GetTicketAsync(session.SessionKey, ct)).ShouldNotBeNull();
        var expiresUtc = ticket.Properties.ExpiresUtc.ShouldNotBeNull();
        expiresUtc.ShouldBeInRange(before + lifetime - Tolerance, after + lifetime + Tolerance);
        ticket.Properties.IsPersistent.ShouldBeTrue();
        session.ExpiresAt.ShouldBe(expiresUtc, TimeSpan.FromSeconds(1));
        rsp.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal)
            && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Session_PastIdentitysSecurityStampValidationInterval_StaysSignedInAsync()
    {
        // The session principal carries no security stamp. With Identity's validator still wired, the first request
        // after its 30-minute interval would sign every session out, whatever the configured lifetime.
        var user = await this.CreateTestUserAsync("stamp_interval_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);

        using (this.App.Services.GetRequiredService<PinnableTimeProvider>().Pin(DateTimeOffset.UtcNow.AddMinutes(45)))
        {
            await ShouldAuthenticateAsync(client, HttpStatusCode.OK, "security-stamp validation must not end the session");
        }

        (await this.GetSingleSessionAsync(user.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task SlidingRenewal_MovesTheTrackedSessionsExpiryAsync()
    {
        // Arrange — more than half the lifetime has passed, so the cookie handler renews the session.
        var lifetime = TimeSpan.FromHours(this.App.Services.GetRequiredService<IOptions<SecuritySettings>>().Value.SessionExpirationHours);
        var user = await this.CreateTestUserAsync("sliding_renewal_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);
        var original = await this.GetSingleSessionAsync(user.Id);
        var renewalInstant = original.CreatedAt + (lifetime * 0.6);

        // Act
        using (this.App.Services.GetRequiredService<PinnableTimeProvider>().Pin(renewalInstant))
        {
            await ShouldAuthenticateAsync(client, HttpStatusCode.OK);
        }

        // Assert
        var renewed = await this.GetSingleSessionAsync(user.Id);
        renewed.SessionKey.ShouldBe(original.SessionKey);
        renewed.ExpiresAt.ShouldBe(renewalInstant + lifetime, TimeSpan.FromSeconds(5));
        await ShouldAuthenticateAsync(client, HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_ForAnAccountThatMayNotHoldASession_CreatesNoSessionAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the host's eligibility rule refuses a locked-out account.
        var user = await this.CreateTestUserAsync("ineligible_sign_in_test@test.local", SeedData.TenantAId);
        await this.LockOutAsync(user.Id);
        var client = this.App.CreateClient();

        // Act
        var (rsp, _) = await client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
            new LoginRequest { Email = user.Email!, Password = SeedData.DefaultPassword });

        // Assert — the sign-in fails before any session exists.
        rsp.IsSuccessStatusCode.ShouldBeFalse();
        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == user.Id, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task SlidingRenewal_ForAnAccountThatMayNoLongerHoldASession_EndsTheSessionAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange — the account was locked out after signing in, by a path that did not end its sessions.
        var lifetime = TimeSpan.FromHours(this.App.Services.GetRequiredService<IOptions<SecuritySettings>>().Value.SessionExpirationHours);
        var user = await this.CreateTestUserAsync("ineligible_renewal_test@test.local", SeedData.TenantAId);
        using var client = await this.SignInAsync(user.Email!);
        var original = await this.GetSingleSessionAsync(user.Id);
        await this.LockOutAsync(user.Id);

        // Before the renewal is due, the session keeps working: eligibility is a renewal-time backstop.
        await ShouldAuthenticateAsync(client, HttpStatusCode.OK);

        // Act
        using (this.App.Services.GetRequiredService<PinnableTimeProvider>().Pin(original.CreatedAt + (lifetime * 0.6)))
        {
            await ShouldAuthenticateAsync(client, HttpStatusCode.Unauthorized, "the renewal must end the session of an ineligible account");
        }

        // Assert — the session is gone, not just refused once.
        await ShouldAuthenticateAsync(client, HttpStatusCode.Unauthorized);
        using var scope = this.App.CreateDbContextScope();
        (await scope.Context.ActiveSessions.AnyAsync(s => s.UserId == user.Id, ct)).ShouldBeFalse();
        (await this.App.Services.GetRequiredService<ITicketCacheService>().GetTicketAsync(original.SessionKey, ct)).ShouldBeNull();
    }

    private async Task LockOutAsync(Guid userId)
    {
        using var scope = this.App.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = (await userManager.FindByIdAsync(userId.ToString())).ShouldNotBeNull();
        (await userManager.SetLockoutEnabledAsync(user, true)).Succeeded.ShouldBeTrue();
        (await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)).Succeeded.ShouldBeTrue();
    }
}
