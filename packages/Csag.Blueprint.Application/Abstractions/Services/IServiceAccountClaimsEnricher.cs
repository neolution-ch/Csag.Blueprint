namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Adds the application's own claims to a service-account principal on every request it authenticates. Register any
/// number of implementations; each runs, in registration order, after the package has built the principal from the
/// account's current state.
/// </summary>
/// <remarks>
/// <para>
/// Service-account tokens carry no claims of their own beyond the account and its session, and are resolved through
/// the session on every request, so this is where per-request claims come from — the token cannot carry them.
/// </para>
/// <para>
/// A failure fails the request: an exception propagates out of authentication.
/// </para>
/// </remarks>
public interface IServiceAccountClaimsEnricher
{
    /// <summary>
    /// Adds claims to <see cref="ServiceAccountClaimsContext.Identity"/>.
    /// </summary>
    /// <param name="context">The account the request authenticated as, and the identity to add to.</param>
    /// <returns>A task that completes once the claims are added.</returns>
    Task EnrichAsync(ServiceAccountClaimsContext context);
}
