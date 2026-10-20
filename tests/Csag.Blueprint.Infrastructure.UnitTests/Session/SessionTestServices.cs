namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// Builds the session registrations the way an application composes them — Identity, the session infrastructure and
/// the cookie sessions — over an in-memory database and mocked ticket cache, so the package's internal wiring is
/// tested through its public registration calls.
/// </summary>
internal static class SessionTestServices
{
    /// <summary>
    /// The session lifetime the built services configure.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// Builds the services.
    /// </summary>
    /// <param name="before">Registrations made before the package's, as an application registering earlier would.</param>
    /// <param name="after">Registrations made after the package's.</param>
    /// <param name="configureCookie">The cookie callback passed to <c>AddBlueprintCookieSessions</c>.</param>
    /// <returns>The service provider.</returns>
    public static ServiceProvider Build(
        Action<IServiceCollection>? before = null,
        Action<IServiceCollection>? after = null,
        Action<CookieBuilder>? configureCookie = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContextFactory<TestDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton(Mock.Of<ITicketCacheService>());
        services.AddSingleton(Mock.Of<IActiveSessionTracker>());
        services.AddScoped(_ => CreateUserManager());
        services.AddScoped(_ => Mock.Of<ITenantAuthorizationResolver>());
        services.AddScoped(_ => Mock.Of<ITenantMembershipChecker>());

        before?.Invoke(services);

        services.AddIdentity<TestUser, TestRole>();
        services.AddBlueprintSessionInfrastructure<TestUser, TestDbContext>(options => options.Lifetime = Lifetime);
        services.AddBlueprintCookieSessions(configureCookie ?? (cookie => cookie.SecurePolicy = CookieSecurePolicy.Always));

        after?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Resolves the built options of a cookie scheme, the session cookie's by default.
    /// </summary>
    /// <param name="services">The built services.</param>
    /// <param name="scheme">The cookie scheme.</param>
    /// <returns>The scheme's options, post-configured.</returns>
    public static CookieAuthenticationOptions GetCookieOptions(this IServiceProvider services, string? scheme = null)
        => services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme ?? IdentityConstants.ApplicationScheme);

    /// <summary>
    /// Creates a request whose services answer the cookie events' lookups.
    /// </summary>
    /// <param name="eligibility">The eligibility rule the events ask.</param>
    /// <param name="authenticationService">The authentication service a sign-out goes through.</param>
    /// <returns>The request.</returns>
    public static DefaultHttpContext CreateHttpContext(ISessionEligibility eligibility, IAuthenticationService? authenticationService = null)
    {
        var requestServices = new ServiceCollection()
            .AddSingleton(eligibility)
            .AddSingleton(authenticationService ?? Mock.Of<IAuthenticationService>())
            .BuildServiceProvider();

        return new DefaultHttpContext { RequestServices = requestServices };
    }

    /// <summary>
    /// Gets the session cookie's scheme as the cookie handler hands it to its events.
    /// </summary>
    /// <returns>The scheme.</returns>
    public static AuthenticationScheme SessionScheme()
        => new(IdentityConstants.ApplicationScheme, displayName: null, typeof(CookieAuthenticationHandler));

    /// <summary>
    /// Creates a session principal for an account, optionally scoped to a tenant.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="tenantId">The tenant, if any.</param>
    /// <returns>The principal.</returns>
    public static ClaimsPrincipal CreatePrincipal(Guid accountId, Guid? tenantId = null)
    {
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, accountId.ToString("D", CultureInfo.InvariantCulture)));
        if (tenantId.HasValue)
        {
            identity.AddClaim(new Claim(IdentityClaimTypes.TenantId, tenantId.Value.ToString("D", CultureInfo.InvariantCulture)));
        }

        return new ClaimsPrincipal(identity);
    }

    private static UserManager<TestUser> CreateUserManager()
    {
        var userStore = new Mock<IUserStore<TestUser>>();
        return new Mock<UserManager<TestUser>>(userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!).Object;
    }
}
