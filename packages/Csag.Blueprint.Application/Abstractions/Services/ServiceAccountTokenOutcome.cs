namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// What <see cref="IServiceAccountCredentials.IssueTokenAsync"/> did.
/// </summary>
public enum ServiceAccountTokenOutcome
{
    /// <summary>
    /// A token was issued. Map to 200.
    /// </summary>
    Issued = 0,

    /// <summary>
    /// No token: the client is unknown, the secret is wrong, or the account is locked out. The cases are deliberately
    /// indistinguishable. Map to 401.
    /// </summary>
    InvalidCredentials = 1,

    /// <summary>
    /// No token: the secret is correct but the account is deactivated. Map to 403.
    /// </summary>
    AccountInactive = 2,
}
