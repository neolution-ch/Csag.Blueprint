namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// The account a <see cref="SessionRevocation"/> is about. The kind keeps user and service-account identifiers
/// apart: both are <see cref="Guid"/>s from different tables, so a participant must not treat one as the other.
/// </summary>
/// <param name="Kind">Whether <paramref name="Id"/> identifies a user or a service account.</param>
/// <param name="Id">The account's identifier.</param>
public readonly record struct SessionSubject(SessionSubjectKind Kind, Guid Id)
{
    /// <summary>
    /// Creates the subject for a user account.
    /// </summary>
    /// <param name="userId">The user's identifier.</param>
    /// <returns>The subject.</returns>
    public static SessionSubject User(Guid userId) => new(SessionSubjectKind.User, userId);

    /// <summary>
    /// Creates the subject for a service account.
    /// </summary>
    /// <param name="serviceAccountId">The service account's identifier.</param>
    /// <returns>The subject.</returns>
    public static SessionSubject ServiceAccount(Guid serviceAccountId) => new(SessionSubjectKind.ServiceAccount, serviceAccountId);
}
