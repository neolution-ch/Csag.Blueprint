namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Service for sending password reset emails to users.
/// </summary>
public interface IPasswordResetEmailService
{
    /// <summary>
    /// Sends a password reset email containing a reset link to the specified email address.
    /// </summary>
    /// <param name="email">The email address to send the reset link to.</param>
    /// <param name="resetLink">The full URL that the user should visit to reset their password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendPasswordResetEmailAsync(string email, string resetLink, CancellationToken cancellationToken = default);
}
