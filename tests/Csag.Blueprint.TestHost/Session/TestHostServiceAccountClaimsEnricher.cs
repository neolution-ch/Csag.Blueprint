namespace Csag.Blueprint.TestHost.Session;

using System.Globalization;
using System.Security.Claims;
using Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Adds one host-specific claim to every service-account principal, standing in for an application's own per-request
/// claims so tests can see the hook run on each request.
/// </summary>
public sealed class TestHostServiceAccountClaimsEnricher : IServiceAccountClaimsEnricher
{
    /// <summary>
    /// The claim type the enricher adds, carrying the service account's id.
    /// </summary>
    public static readonly string ClaimType = "testhost:service-account";

    /// <inheritdoc/>
    public Task EnrichAsync(ServiceAccountClaimsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Identity.AddClaim(new Claim(ClaimType, context.ServiceAccountId.ToString("D", CultureInfo.InvariantCulture)));
        return Task.CompletedTask;
    }
}
