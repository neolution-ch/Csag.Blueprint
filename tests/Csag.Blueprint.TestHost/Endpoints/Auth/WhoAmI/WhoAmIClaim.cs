namespace Csag.Blueprint.TestHost.Endpoints.Auth.WhoAmI;

/// <summary>
/// One claim of the caller's principal.
/// </summary>
public sealed class WhoAmIClaim
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
