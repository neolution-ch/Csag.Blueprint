namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;
using Csag.Blueprint.Application.Claims;
using Csag.Blueprint.Infrastructure.Extensions;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves a validated service-account token to its session and builds the request's principal from the account's
/// current state.
/// </summary>
internal static class ServiceAccountTokenValidation
{
    /// <summary>
    /// Runs once the token's signature, algorithm, issuer, audience and lifetime are validated. Refuses the token when
    /// it names no session, when the session is gone or expired, when the account is deleted or inactive, or when the
    /// session belongs to an account other than the token's subject. Otherwise replaces the token's principal with
    /// one built from the account: its client id, name, session, tenant, roles and permissions, then whatever the
    /// application's <see cref="IServiceAccountClaimsEnricher"/>s add.
    /// </summary>
    /// <param name="context">The handler's context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var tokenPrincipal = context.Principal;
        var sessionKey = tokenPrincipal?.FindFirstValue(ServiceAccountClaimTypes.SessionId);
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            context.Fail("The service-account token names no session.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var sessions = services.GetRequiredService<IServiceAccountSessionManager>();
        var session = await sessions.ValidateSessionAsync(sessionKey, context.HttpContext.RequestAborted);
        if (session is null)
        {
            context.Fail("The service-account session is no longer valid.");
            return;
        }

        if (!string.Equals(tokenPrincipal!.FindFirstValue(ServiceAccountClaimTypes.Subject), session.ClientId, StringComparison.Ordinal))
        {
            context.Fail("The service-account token's subject does not own its session.");
            return;
        }

        // A new identity rather than the token's: only what the package resolves server-side reaches the principal.
        var identity = new ClaimsIdentity(context.Scheme.Name, ServiceAccountClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ServiceAccountClaimTypes.Subject, session.ClientId));
        identity.AddClaim(new Claim(ServiceAccountClaimTypes.Name, session.Name));
        identity.AddClaim(new Claim(ServiceAccountClaimTypes.Type, ServiceAccountClaimTypes.ServiceAccountType));
        identity.AddClaim(new Claim(ServiceAccountClaimTypes.SessionId, sessionKey));
        identity.SetTenantClaim(session.TenantId);
        identity.SetAuthorizationClaims(session.Roles.ToList(), session.Permissions.ToList());

        foreach (var enricher in services.GetServices<IServiceAccountClaimsEnricher>())
        {
            await enricher.EnrichAsync(new ServiceAccountClaimsContext
            {
                ServiceAccountId = session.ServiceAccountId,
                TenantId = session.TenantId,
                Identity = identity,
            });
        }

        context.Principal = new ClaimsPrincipal(identity);
    }
}
