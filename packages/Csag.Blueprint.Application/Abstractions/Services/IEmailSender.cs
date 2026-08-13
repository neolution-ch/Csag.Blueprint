namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// General-purpose abstraction for sending transactional emails.
/// This is the primary email sending mechanism; feature-specific services (for example password reset)
/// compose their message content and dispatch it through this sender. Implementations decide the transport
/// (for example SMTP for deployed environments, or console logging for local development).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email message.
    /// </summary>
    /// <param name="recipient">The recipient email address.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="htmlBody">The HTML body of the message.</param>
    /// <param name="textBody">The plain-text alternative body of the message, shown by clients that do not render HTML.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendEmailAsync(string recipient, string subject, string htmlBody, string textBody, CancellationToken cancellationToken = default);
}
