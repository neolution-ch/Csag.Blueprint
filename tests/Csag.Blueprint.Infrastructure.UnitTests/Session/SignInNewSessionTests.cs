namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="SessionHttpContextExtensions.SignInNewSessionAsync"/> through the real cookie handler and
/// ticket store, over an in-memory ticket cache: whatever state the request's session cookie is in, the new session is
/// stored under a new key and the old key no longer holds a ticket.
/// </summary>
public sealed class SignInNewSessionTests
{
    private const string SessionIdClaim = "Microsoft.AspNetCore.Authentication.Cookies-SessionId";

    private static readonly Guid OldAccountId = new Guid("2a4c6e80-1b3d-4f5a-8c7e-9d0f1a2b3c4d");
    private static readonly Guid NewAccountId = new Guid("5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9");

    [Fact]
    public async Task OverALiveSession_StoresTheNewSessionUnderANewKeyAndRemovesTheOldOne()
    {
        var cache = new InMemoryTicketCache();
        using var services = SessionTestServices.Build(after: s => s.AddSingleton<ITicketCacheService>(cache));
        var (httpContext, oldKey) = await RequestWithSessionCookieAsync(services);
        (await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme)).Succeeded.ShouldBeTrue();

        await httpContext.SignInNewSessionAsync(SessionTestServices.CreatePrincipal(NewAccountId));

        ShouldHoldOnlyTheNewSession(cache, oldKey);
    }

    [Fact]
    public async Task OverASessionAnApplicationHandlerRejected_StillStoresTheNewSessionUnderANewKey()
    {
        // An application's OnValidatePrincipal can reject the principal without ending the session, so the request
        // does not authenticate while the ticket is still in the cache under the cookie's key.
        var cache = new InMemoryTicketCache();
        using var services = SessionTestServices.Build(after: s =>
        {
            s.AddSingleton<ITicketCacheService>(cache);
            s.ConfigureApplicationCookie(options => options.Events.OnValidatePrincipal = context =>
            {
                context.RejectPrincipal();
                return Task.CompletedTask;
            });
        });
        var (httpContext, oldKey) = await RequestWithSessionCookieAsync(services);
        (await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme)).Succeeded.ShouldBeFalse();

        await httpContext.SignInNewSessionAsync(SessionTestServices.CreatePrincipal(NewAccountId));

        ShouldHoldOnlyTheNewSession(cache, oldKey);
    }

    [Fact]
    public async Task WithoutASessionCookie_StoresTheNewSessionAndClearsNoCookie()
    {
        var cache = new InMemoryTicketCache();
        using var services = SessionTestServices.Build(after: s => s.AddSingleton<ITicketCacheService>(cache));
        var httpContext = new DefaultHttpContext { RequestServices = services.CreateScope().ServiceProvider };

        await httpContext.SignInNewSessionAsync(SessionTestServices.CreatePrincipal(NewAccountId));

        cache.Tickets.ShouldHaveSingleItem();
        var sessionCookies = httpContext.Response.Headers.SetCookie
            .Where(c => c!.StartsWith(services.GetCookieOptions().Cookie.Name + "=", StringComparison.Ordinal))
            .ToList();
        sessionCookies.ShouldHaveSingleItem().ShouldNotStartWith(services.GetCookieOptions().Cookie.Name + "=;");
    }

    private static async Task<(DefaultHttpContext HttpContext, string OldKey)> RequestWithSessionCookieAsync(ServiceProvider services)
    {
        var options = services.GetCookieOptions();
        var oldKey = await options.SessionStore.ShouldNotBeNull().StoreAsync(new AuthenticationTicket(
            SessionTestServices.CreatePrincipal(OldAccountId),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) },
            IdentityConstants.ApplicationScheme));

        // The cookie carries only the session key, as the cookie handler writes it when a session store is set.
        var cookieTicket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(SessionIdClaim, oldKey)], IdentityConstants.ApplicationScheme)),
            IdentityConstants.ApplicationScheme);

        var httpContext = new DefaultHttpContext { RequestServices = services.CreateScope().ServiceProvider };
        httpContext.Request.Headers.Cookie = $"{options.Cookie.Name}={options.TicketDataFormat.Protect(cookieTicket)}";
        return (httpContext, oldKey);
    }

    private static void ShouldHoldOnlyTheNewSession(InMemoryTicketCache cache, string oldKey)
    {
        cache.Tickets.ShouldNotContainKey(oldKey, "the old key must hold no ticket, or a copy of the old cookie reaches it");
        var (newKey, ticket) = cache.Tickets.ShouldHaveSingleItem();
        newKey.ShouldNotBe(oldKey);
        ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(NewAccountId.ToString("D", CultureInfo.InvariantCulture));
    }

    private sealed class InMemoryTicketCache : ITicketCacheService
    {
        public Dictionary<string, AuthenticationTicket> Tickets { get; } = new(StringComparer.Ordinal);

        public Task<AuthenticationTicket?> GetTicketAsync(string sessionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Tickets.TryGetValue(sessionKey, out var ticket) ? ticket : null);

        public Task SetTicketAsync(string sessionKey, AuthenticationTicket ticket, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
        {
            this.Tickets[sessionKey] = ticket;
            return Task.CompletedTask;
        }

        public Task RemoveTicketAsync(string sessionKey, CancellationToken cancellationToken = default)
        {
            this.Tickets.Remove(sessionKey);
            return Task.CompletedTask;
        }
    }
}
