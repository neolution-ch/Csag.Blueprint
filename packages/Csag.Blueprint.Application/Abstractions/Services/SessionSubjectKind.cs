namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The kind of account a <see cref="SessionSubject"/> identifies.
/// </summary>
public enum SessionSubjectKind
{
    /// <summary>
    /// A user account, signed in through the session cookie.
    /// </summary>
    User = 0,

    /// <summary>
    /// A service account, authenticating with a bearer token.
    /// </summary>
    ServiceAccount = 1,
}
