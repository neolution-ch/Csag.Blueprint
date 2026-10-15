namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>
/// Unit tests for <c>AddBlueprintCookieSessions</c>: the session cookie's fixed behaviour (lifetime, sliding expiration,
/// HTTP-only, 401/403, no security-stamp validation), the application's cookie callback and event handlers running
/// alongside the package's, and the session eligibility checks at sign-in and sliding renewal.
/// </summary>
public sealed class CookieSessionsTests
{
    private static readonly Guid AccountId = new Guid("6b9b1c55-4d4f-4f1e-9a39-1f1d2f9c0a01");

    [Fact]
    public void SessionCookie_AppliesTheCallbackThenReappliesThePackagesInvariants()
    {
        // Arrange — the callback and a later application Configure both try to change what the package owns.
        using var services = SessionTestServices.Build(
            configureCookie: cookie =>
            {
                cookie.Name = "app.session";
                cookie.SameSite = SameSiteMode.Strict;
                cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.HttpOnly = false;
            },
            after: s => s.ConfigureApplicationCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromMinutes(1);
                options.SlidingExpiration = false;
                options.SessionStore = null;
            }));

        // Act
        var options = services.GetCookieOptions();

        // Assert — the callback shapes the cookie...
        options.Cookie.Name.ShouldBe("app.session");
        options.Cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        options.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);

        // ...while the session it carries stays the package's.
        options.Cookie.HttpOnly.ShouldBeTrue();
        options.ExpireTimeSpan.ShouldBe(SessionTestServices.Lifetime);
        options.SlidingExpiration.ShouldBeTrue();
        options.SessionStore.ShouldBeSameAs(services.GetRequiredService<ITicketStore>());
    }

    [Fact]
    public void OtherCookieSchemes_KeepTheFrameworkBehaviour()
    {
        using var services = SessionTestServices.Build();

        var external = services.GetCookieOptions(IdentityConstants.ExternalScheme);

        external.SessionStore.ShouldBeNull();
        external.Events.OnRedirectToLogin.Method.ShouldBe(new CookieAuthenticationEvents().OnRedirectToLogin.Method);
    }

    [Fact]
    public async Task ValidatePrincipal_AStampLessPrincipalPastTheValidationInterval_IsKept()
    {
        // Arrange — AddIdentity validates the application cookie's security stamp every 30 minutes. The session
        // principal carries no stamp claim, and IssuedUtc lies far in the past, so the validator — were it still
        // wired — would reject it (or, resolving its Identity services, fail).
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var ticket = new AuthenticationTicket(
            SessionTestServices.CreatePrincipal(AccountId),
            new AuthenticationProperties { IssuedUtc = DateTimeOffset.UnixEpoch },
            IdentityConstants.ApplicationScheme);
        using var scope = services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var context = new CookieValidatePrincipalContext(httpContext, SessionTestServices.SessionScheme(), options, ticket);

        // Act
        await options.Events.ValidatePrincipal(context);

        // Assert
        context.Principal.ShouldNotBeNull("a stamp-less session principal must survive security-stamp validation");
    }

    [Fact]
    public async Task ValidatePrincipal_RegisteredBeforeIdentity_StillDropsTheStampValidator()
    {
        // Arrange — the post-configure step runs after AddIdentity's Configure whichever is registered first.
        using var services = SessionTestServices.Build(before: s => s.AddBlueprintCookieSessions(cookie => cookie.SecurePolicy = CookieSecurePolicy.Always));
        var options = services.GetCookieOptions();
        var ticket = new AuthenticationTicket(
            SessionTestServices.CreatePrincipal(AccountId),
            new AuthenticationProperties { IssuedUtc = DateTimeOffset.UnixEpoch },
            IdentityConstants.ApplicationScheme);
        using var scope = services.CreateScope();
        var context = new CookieValidatePrincipalContext(
            new DefaultHttpContext { RequestServices = scope.ServiceProvider }, SessionTestServices.SessionScheme(), options, ticket);

        // Act
        await options.Events.ValidatePrincipal(context);

        // Assert
        context.Principal.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidatePrincipal_ApplicationHandler_StillRuns()
    {
        // Arrange
        var applicationHandlerRan = false;
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
            options.Events.OnValidatePrincipal = _ =>
            {
                applicationHandlerRan = true;
                return Task.CompletedTask;
            }));
        var options = services.GetCookieOptions();
        var context = new CookieValidatePrincipalContext(
            SessionTestServices.CreateHttpContext(Mock.Of<ISessionEligibility>()),
            SessionTestServices.SessionScheme(),
            options,
            new AuthenticationTicket(SessionTestServices.CreatePrincipal(AccountId), IdentityConstants.ApplicationScheme));

        // Act
        await options.Events.ValidatePrincipal(context);

        // Assert
        applicationHandlerRan.ShouldBeTrue();
        context.Principal.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(false, StatusCodes.Status401Unauthorized)]
    [InlineData(true, StatusCodes.Status403Forbidden)]
    public async Task Redirects_AreAnsweredWithAStatusCodeInstead(bool accessDenied, int expectedStatus)
    {
        // Arrange
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var context = CreateRedirectContext(options);

        // Act
        await (accessDenied ? options.Events.RedirectToAccessDenied(context) : options.Events.RedirectToLogin(context));

        // Assert
        context.Response.StatusCode.ShouldBe(expectedStatus);
        context.Response.Headers.Location.ShouldBeEmpty();
    }

    [Fact]
    public async Task RedirectToLogin_ApplicationHandlerRunsAndThe401Stands()
    {
        // Arrange — an application handler that both records and redirects; the package's status code still wins.
        var applicationHandlerRan = false;
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
            options.Events.OnRedirectToLogin = context =>
            {
                applicationHandlerRan = true;
                context.Response.Redirect("/login");
                return Task.CompletedTask;
            }));
        var options = services.GetCookieOptions();
        var context = CreateRedirectContext(options);

        // Act
        await options.Events.RedirectToLogin(context);

        // Assert
        applicationHandlerRan.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.Location.ShouldBeEmpty();
    }

    [Fact]
    public void SessionCookie_WithEventsType_FailsWhenItsOptionsAreBuilt()
    {
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
            options.EventsType = typeof(CookieAuthenticationEvents)));

        var exception = Should.Throw<InvalidOperationException>(() => services.GetCookieOptions());

        exception.Message.ShouldContain("sets EventsType");
    }

    [Fact]
    public void SessionCookie_WithEventsOverridingAnEventTheSessionReliesOn_FailsWhenItsOptionsAreBuilt()
    {
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
            options.Events = new ValidatePrincipalOverridingEvents()));

        var exception = Should.Throw<InvalidOperationException>(() => services.GetCookieOptions());

        exception.Message.ShouldContain($"uses {nameof(ValidatePrincipalOverridingEvents)}, which overrides ValidatePrincipal");
    }

    [Fact]
    public async Task SigningIn_MakesTheCookiePersistentAndRunsTheApplicationHandler()
    {
        // Arrange
        var applicationHandlerRan = false;
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
            options.Events.OnSigningIn = _ =>
            {
                applicationHandlerRan = true;
                return Task.CompletedTask;
            }));
        var options = services.GetCookieOptions();
        var context = CreateSigningInContext(options, Eligibility(true));

        // Act
        await options.Events.SigningIn(context);

        // Assert
        context.Properties.IsPersistent.ShouldBeTrue();
        applicationHandlerRan.ShouldBeTrue();
    }

    [Fact]
    public async Task SigningIn_AnAccountThatMayNotHoldASession_Throws()
    {
        // Arrange
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var eligibility = Eligibility(false);
        var context = CreateSigningInContext(options, eligibility);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => options.Events.SigningIn(context));
        Mock.Get(eligibility).Verify(e => e.MayHoldSessionAsync(AccountId), Times.Once);
    }

    [Fact]
    public async Task SigningIn_RegisteredTwice_AsksEligibilityOnce()
    {
        // Two registration calls contribute their callbacks to one post-configure step, so the package's handlers
        // are composed once rather than once per call.
        var secondCallbackRan = false;
        using var services = SessionTestServices.Build(after: s => s.AddBlueprintCookieSessions(_ => secondCallbackRan = true));
        var options = services.GetCookieOptions();
        var eligibility = Eligibility(true);

        await options.Events.SigningIn(CreateSigningInContext(options, eligibility));

        secondCallbackRan.ShouldBeTrue();
        Mock.Get(eligibility).Verify(e => e.MayHoldSessionAsync(AccountId), Times.Once);
    }

    [Fact]
    public async Task SigningIn_SeveralApplicationHandlersOnOneEvent_AreEachAwaitedBeforeThePackages()
    {
        // Arrange — two handlers added to the same event; the first only finishes once the gate opens.
        var order = new List<string>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
        {
            options.Events.OnSigningIn += async _ =>
            {
                await gate.Task;
                order.Add("first");
            };
            options.Events.OnSigningIn += _ =>
            {
                order.Add("second");
                return Task.CompletedTask;
            };
        }));
        var options = services.GetCookieOptions();
        var eligibility = new Mock<ISessionEligibility>();
        eligibility.Setup(e => e.MayHoldSessionAsync(AccountId)).Callback(() => order.Add("package")).ReturnsAsync(true);

        // Act
        var signingIn = options.Events.SigningIn(CreateSigningInContext(options, eligibility.Object));
        gate.SetResult();
        await signingIn;

        // Assert
        order.ShouldBe(["first", "second", "package"]);
    }

    [Fact]
    public async Task SlidingRenewal_AnAccountThatMayNoLongerHoldASession_EndsTheSession()
    {
        // Arrange
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var authentication = new Mock<IAuthenticationService>();
        var httpContext = SessionTestServices.CreateHttpContext(Eligibility(false), authentication.Object);
        var ticket = new AuthenticationTicket(SessionTestServices.CreatePrincipal(AccountId), IdentityConstants.ApplicationScheme);
        var sliding = new CookieSlidingExpirationContext(
            httpContext, SessionTestServices.SessionScheme(), options, ticket, TimeSpan.FromHours(5), TimeSpan.FromHours(3))
        {
            ShouldRenew = true,
        };
        var validate = new CookieValidatePrincipalContext(httpContext, SessionTestServices.SessionScheme(), options, ticket);

        // Act — the cookie handler raises both events in this order within one authentication pass.
        await options.Events.CheckSlidingExpiration(sliding);
        await options.Events.ValidatePrincipal(validate);

        // Assert — no renewal, no principal, and a sign-out that removes the session and clears the cookie.
        sliding.ShouldRenew.ShouldBeFalse();
        validate.Principal.ShouldBeNull();
        authentication.Verify(
            a => a.SignOutAsync(httpContext, IdentityConstants.ApplicationScheme, It.IsAny<AuthenticationProperties?>()),
            Times.Once);
    }

    [Fact]
    public async Task SlidingRenewal_RefusedWhenAnApplicationHandlerAlreadyRejectedThePrincipal_StillEndsTheSession()
    {
        // Arrange — the application's handler runs first and rejects the principal, which ends nothing server-side.
        using var services = SessionTestServices.Build(after: s => s.ConfigureApplicationCookie(options =>
            options.Events.OnValidatePrincipal = context =>
            {
                context.RejectPrincipal();
                return Task.CompletedTask;
            }));
        var options = services.GetCookieOptions();
        var authentication = new Mock<IAuthenticationService>();
        var httpContext = SessionTestServices.CreateHttpContext(Eligibility(false), authentication.Object);
        var ticket = new AuthenticationTicket(SessionTestServices.CreatePrincipal(AccountId), IdentityConstants.ApplicationScheme);
        var sliding = new CookieSlidingExpirationContext(
            httpContext, SessionTestServices.SessionScheme(), options, ticket, TimeSpan.FromHours(5), TimeSpan.FromHours(3))
        {
            ShouldRenew = true,
        };
        var validate = new CookieValidatePrincipalContext(httpContext, SessionTestServices.SessionScheme(), options, ticket);

        // Act
        await options.Events.CheckSlidingExpiration(sliding);
        await options.Events.ValidatePrincipal(validate);

        // Assert
        validate.Principal.ShouldBeNull();
        authentication.Verify(
            a => a.SignOutAsync(httpContext, IdentityConstants.ApplicationScheme, It.IsAny<AuthenticationProperties?>()),
            Times.Once);
    }

    [Fact]
    public async Task SlidingRenewal_AnEligibleAccount_KeepsTheRenewalAndThePrincipal()
    {
        // Arrange
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var authentication = new Mock<IAuthenticationService>();
        var httpContext = SessionTestServices.CreateHttpContext(Eligibility(true), authentication.Object);
        var ticket = new AuthenticationTicket(SessionTestServices.CreatePrincipal(AccountId), IdentityConstants.ApplicationScheme);
        var sliding = new CookieSlidingExpirationContext(
            httpContext, SessionTestServices.SessionScheme(), options, ticket, TimeSpan.FromHours(5), TimeSpan.FromHours(3))
        {
            ShouldRenew = true,
        };
        var validate = new CookieValidatePrincipalContext(httpContext, SessionTestServices.SessionScheme(), options, ticket);

        // Act
        await options.Events.CheckSlidingExpiration(sliding);
        await options.Events.ValidatePrincipal(validate);

        // Assert
        sliding.ShouldRenew.ShouldBeTrue();
        validate.Principal.ShouldNotBeNull();
        authentication.Verify(
            a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()),
            Times.Never);
    }

    [Fact]
    public async Task SlidingCheck_WithNoRenewalDue_DoesNotAskEligibility()
    {
        using var services = SessionTestServices.Build();
        var options = services.GetCookieOptions();
        var eligibility = Eligibility(false);
        var httpContext = SessionTestServices.CreateHttpContext(eligibility);
        var ticket = new AuthenticationTicket(SessionTestServices.CreatePrincipal(AccountId), IdentityConstants.ApplicationScheme);
        var sliding = new CookieSlidingExpirationContext(
            httpContext, SessionTestServices.SessionScheme(), options, ticket, TimeSpan.FromHours(1), TimeSpan.FromHours(7))
        {
            ShouldRenew = false,
        };

        await options.Events.CheckSlidingExpiration(sliding);

        Mock.Get(eligibility).Verify(e => e.MayHoldSessionAsync(It.IsAny<Guid>()), Times.Never);
    }

    private static ISessionEligibility Eligibility(bool mayHoldSession)
    {
        var eligibility = new Mock<ISessionEligibility>();
        eligibility.Setup(e => e.MayHoldSessionAsync(It.IsAny<Guid>())).ReturnsAsync(mayHoldSession);
        return eligibility.Object;
    }

    private static CookieSigningInContext CreateSigningInContext(CookieAuthenticationOptions options, ISessionEligibility eligibility)
    {
        return new CookieSigningInContext(
            SessionTestServices.CreateHttpContext(eligibility),
            SessionTestServices.SessionScheme(),
            options,
            SessionTestServices.CreatePrincipal(AccountId),
            new AuthenticationProperties { IsPersistent = false },
            new CookieOptions());
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirectContext(CookieAuthenticationOptions options)
    {
        return new RedirectContext<CookieAuthenticationOptions>(
            SessionTestServices.CreateHttpContext(Mock.Of<ISessionEligibility>()),
            SessionTestServices.SessionScheme(),
            options,
            new AuthenticationProperties(),
            "/Account/Login?ReturnUrl=%2F");
    }

    /// <summary>
    /// An events type that overrides <see cref="CookieAuthenticationEvents.ValidatePrincipal"/>, so the cookie handler
    /// would never reach the delegate the package installs there.
    /// </summary>
    private sealed class ValidatePrincipalOverridingEvents : CookieAuthenticationEvents
    {
        public override Task ValidatePrincipal(CookieValidatePrincipalContext context) => Task.CompletedTask;
    }
}

