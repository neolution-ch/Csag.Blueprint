namespace Csag.Blueprint.Infrastructure.Authentication;

/// <summary>
/// The token kinds behind the <see cref="BlueprintAuthenticationSchemes.Bearer"/> selector. The package registers
/// service-account tokens; an application adds its own kinds here, each validated by its own scheme.
/// </summary>
/// <remarks>
/// For every request with a bearer token the selector asks the kinds in the order they were added and forwards the
/// request to the first whose predicate accepts the token; when none does, to
/// <see cref="BlueprintAuthenticationSchemes.ServiceAccountToken"/>. One handler authenticates the request either way.
/// </remarks>
public sealed class BlueprintBearerOptions
{
    private readonly List<BearerTokenKind> tokenKinds = [];

    /// <summary>
    /// Gets the token kinds an application added, in the order the selector asks them.
    /// </summary>
    public IReadOnlyList<BearerTokenKind> TokenKinds => this.tokenKinds;

    /// <summary>
    /// Adds a token kind.
    /// </summary>
    /// <param name="scheme">The scheme that validates tokens of this kind. It must not be one of the selectors.</param>
    /// <param name="matchesToken">Decides from the raw token whether it is of this kind, typically by its issuer or
    /// type header. Runs on every bearer request before the token is validated, so it must return
    /// <see langword="false"/> for a token it cannot read rather than throw.</param>
    /// <returns>These options, for chaining.</returns>
    public BlueprintBearerOptions AddTokenKind(string scheme, Func<string, bool> matchesToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentNullException.ThrowIfNull(matchesToken);

        if (string.Equals(scheme, BlueprintAuthenticationSchemes.Bearer, StringComparison.Ordinal)
            || string.Equals(scheme, BlueprintAuthenticationSchemes.SessionOrBearer, StringComparison.Ordinal))
        {
            throw new ArgumentException($"A token kind cannot be validated by the selector scheme '{scheme}'.", nameof(scheme));
        }

        this.tokenKinds.Add(new BearerTokenKind(scheme, matchesToken));
        return this;
    }
}
