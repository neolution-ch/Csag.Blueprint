namespace Csag.Blueprint.TestHost.Endpoints.Auth.Token;

/// <summary>
/// An issued service-account bearer token.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token type, always "Bearer".
    /// </summary>
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token's lifetime in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }
}
