namespace Csag.Blueprint.TestHost.Endpoints.Auth.ChangePassword;

/// <summary>
/// The caller's current password and the one replacing it.
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>
    /// Gets or sets the current password.
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new password.
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}
