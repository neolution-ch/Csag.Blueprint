namespace Csag.Blueprint.TestHost.Extensions;

using Csag.Blueprint.Application.Abstractions.Authorization;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Infrastructure.Authorization;
using Csag.Blueprint.Infrastructure.Session;
using Csag.Blueprint.TestHost.Session;
using Csag.Blueprint.Tests.Shared.Authorization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Entities;
using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Registers distributed session authentication the way a consuming application does: the package's session
/// infrastructure and cookie sessions, plus the host's own hooks — the session eligibility rule, a revocation
/// participant, and the permission claims transformation driven by <see cref="TestRolePermissionResolver"/>.
/// </summary>
public static class TestHostSessionExtensions
{
    /// <summary>
    /// The edge-stamped client address header the host trusts, as a deployment behind a load balancer would.
    /// </summary>
    public static readonly string ClientIpHeader = "X-Client-IP";

    /// <summary>
    /// Adds the Blueprint session infrastructure and cookie sessions, the host's session hooks, and the
    /// role-to-permission resolver and claims transformation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cookieSecurePolicy">The cookie secure policy for the session cookie.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTestHostSessionAuthentication(
        this IServiceCollection services, CookieSecurePolicy cookieSecurePolicy)
    {
        services.AddBlueprintSessionInfrastructure<TestUser, TestDbContext>(options => options.ClientIpHeader = ClientIpHeader);
        services.AddBlueprintCookieSessions(cookie =>
        {
            cookie.SecurePolicy = cookieSecurePolicy;
            cookie.SameSite = SameSiteMode.Lax;
        });

        services.AddScoped<ISessionEligibility, TestHostSessionEligibility>();
        services.AddSingleton<RecordingRevocationParticipant>();
        services.AddSingleton<ISessionRevocationParticipant>(sp => sp.GetRequiredService<RecordingRevocationParticipant>());

        // Stateless resolver mapping the shared test roles to their permissions; singleton on purpose.
        services.AddSingleton<IRolePermissionResolver, TestRolePermissionResolver>();

        // Adds permission claims derived from role claims after authentication on every request.
        services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();

        return services;
    }
}
