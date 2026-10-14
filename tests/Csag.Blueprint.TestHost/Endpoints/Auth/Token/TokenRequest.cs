namespace Csag.Blueprint.TestHost.Endpoints.Auth.Token;

/// <summary>
/// The client credentials a service account authenticates with.
/// </summary>
public sealed class TokenRequest
{
    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
