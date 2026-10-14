namespace Csag.Blueprint.Infrastructure.Authentication;

/// <summary>
/// A kind of bearer token behind the <see cref="BlueprintAuthenticationSchemes.Bearer"/> selector. See
/// <see cref="BlueprintBearerOptions.AddTokenKind"/>.
/// </summary>
/// <param name="Scheme">The scheme that validates tokens of this kind.</param>
/// <param name="MatchesToken">Decides from the raw token whether it is of this kind.</param>
public sealed record BearerTokenKind(string Scheme, Func<string, bool> MatchesToken);
