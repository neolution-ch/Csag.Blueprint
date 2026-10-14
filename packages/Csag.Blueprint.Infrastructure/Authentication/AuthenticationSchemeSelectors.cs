namespace Csag.Blueprint.Infrastructure.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// The forward selectors of the two policy schemes. Each names exactly one target scheme per request, so each request
/// is authenticated by exactly one scheme.
/// </summary>
internal static class AuthenticationSchemeSelectors
{
    /// <summary>
    /// Selects the handler of the request's bearer token kind for <see cref="BlueprintAuthenticationSchemes.Bearer"/>.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The scheme of the first token kind that accepts the token, or the service-account scheme.</returns>
    public static string SelectTokenKind(HttpContext context)
    {
        if (context.Request.TryGetBearerToken(out var token))
        {
            var bearerOptions = context.RequestServices.GetRequiredService<IOptions<BlueprintBearerOptions>>().Value;
            foreach (var kind in bearerOptions.TokenKinds)
            {
                if (kind.MatchesToken(token))
                {
                    return kind.Scheme;
                }
            }
        }

        // Also the target for a request without a token, so the challenge it gets is the bearer challenge.
        return BlueprintAuthenticationSchemes.ServiceAccountToken;
    }

    /// <summary>
    /// Selects between the bearer token selector and the session cookie for
    /// <see cref="BlueprintAuthenticationSchemes.SessionOrBearer"/>.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The bearer selector when the request carries a bearer token; otherwise the session cookie scheme.</returns>
    public static string SelectSessionOrBearer(HttpContext context)
        => context.Request.TryGetBearerToken(out _)
            ? BlueprintAuthenticationSchemes.Bearer
            : IdentityConstants.ApplicationScheme;
}
