namespace Csag.Blueprint.TestHost.Endpoints.Auth.Logout;

/// <summary>
/// Confirmation returned by the logout endpoint.
/// </summary>
public sealed class LogoutResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the session was terminated.
    /// </summary>
    public bool Succeeded { get; set; }
}
