namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Service for sending user-onboarding invitation emails.
/// </summary>
/// <remarks>
/// This mirrors <see cref="IPasswordResetEmailService"/>: it is a thin feature-specific composer that owns the
/// invitation message content and delegates delivery to the shared <see cref="IEmailSender"/> abstraction.
/// </remarks>
public interface IInvitationEmailService
{
    /// <summary>
    /// Sends an invitation email containing the accept-invite link to the specified email address.
    /// </summary>
    /// <param name="email">The email address the invitation was issued to.</param>
    /// <param name="inviteLink">The full URL the invitee should visit to accept the invitation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendInvitationEmailAsync(string email, string inviteLink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies an existing account holder that they have been added to a tenant.
    /// </summary>
    /// <remarks>
    /// This is a <b>notice, not a request</b>. Someone who already holds an account is added to a tenant
    /// immediately, because joining is a staffing decision taken elsewhere rather than something to ask
    /// permission for. The mail exists so that a grant nobody expected still gets noticed by the person it
    /// affects — which is the control that replaces an acceptance step.
    /// </remarks>
    /// <param name="email">The account holder's email address.</param>
    /// <param name="tenantName">The tenant they were added to.</param>
    /// <param name="addedBy">The display name of the admin who added them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendAddedToTenantEmailAsync(string email, string tenantName, string addedBy, CancellationToken cancellationToken = default);
}
