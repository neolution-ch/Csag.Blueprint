namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// A service account's client id and plaintext secret, as handed out once when the credentials are created or the
/// secret is rotated.
/// </summary>
/// <param name="ClientId">The client id the account authenticates with.</param>
/// <param name="ClientSecret">The secret. Only its hash is stored; show it to the caller and discard it.</param>
public sealed record ServiceAccountClientCredentials(string ClientId, string ClientSecret)
{
    /// <inheritdoc/>
    public override string ToString() => $"{nameof(ServiceAccountClientCredentials)} {{ {nameof(this.ClientId)} = {this.ClientId} }}";
}
