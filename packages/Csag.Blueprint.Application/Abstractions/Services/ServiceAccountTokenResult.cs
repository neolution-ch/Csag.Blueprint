namespace Csag.Blueprint.Application.Abstractions.Services;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The result of <see cref="IServiceAccountCredentials.IssueTokenAsync"/>. When a token was issued, its fields map
/// one-to-one onto an OAuth-style token response: <c>access_token</c>, <c>token_type</c> and <c>expires_in</c>.
/// </summary>
public sealed class ServiceAccountTokenResult
{
    /// <summary>
    /// The value of <see cref="TokenType"/>.
    /// </summary>
    public static readonly string BearerTokenType = "Bearer";

    private ServiceAccountTokenResult(ServiceAccountTokenOutcome outcome, string? accessToken, int expiresIn, DateTimeOffset? expiresAt)
    {
        this.Outcome = outcome;
        this.AccessToken = accessToken;
        this.ExpiresIn = expiresIn;
        this.ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Gets what happened.
    /// </summary>
    public ServiceAccountTokenOutcome Outcome { get; }

    /// <summary>
    /// Gets a value indicating whether a token was issued.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AccessToken))]
    public bool IsIssued => this.Outcome == ServiceAccountTokenOutcome.Issued;

    /// <summary>
    /// Gets the token, or <see langword="null"/> when none was issued.
    /// </summary>
    public string? AccessToken { get; }

    /// <summary>
    /// Gets the token type, always <see cref="BearerTokenType"/>.
    /// </summary>
    public string TokenType { get; } = BearerTokenType;

    /// <summary>
    /// Gets the token's lifetime in seconds, or zero when none was issued.
    /// </summary>
    public int ExpiresIn { get; }

    /// <summary>
    /// Gets when the token expires, or <see langword="null"/> when none was issued.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Creates the result of an issued token.
    /// </summary>
    /// <param name="accessToken">The token.</param>
    /// <param name="expiresIn">The token's lifetime in seconds.</param>
    /// <param name="expiresAt">When the token expires.</param>
    /// <returns>The result.</returns>
    public static ServiceAccountTokenResult Issued(string accessToken, int expiresIn, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return new ServiceAccountTokenResult(ServiceAccountTokenOutcome.Issued, accessToken, expiresIn, expiresAt);
    }

    /// <summary>
    /// Creates the result of a refusal.
    /// </summary>
    /// <param name="outcome">Why no token was issued; anything but <see cref="ServiceAccountTokenOutcome.Issued"/>.</param>
    /// <returns>The result.</returns>
    public static ServiceAccountTokenResult Refused(ServiceAccountTokenOutcome outcome)
    {
        if (outcome == ServiceAccountTokenOutcome.Issued || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "A refusal needs a refusal outcome.");
        }

        return new ServiceAccountTokenResult(outcome, accessToken: null, expiresIn: 0, expiresAt: null);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ServiceAccountTokenResult)} {{ {nameof(this.Outcome)} = {this.Outcome} }}";
}
