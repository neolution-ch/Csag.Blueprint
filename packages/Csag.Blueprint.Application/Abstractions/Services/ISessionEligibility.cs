namespace Csag.Blueprint.Application.Abstractions.Services;

/// <summary>
/// Answers "may this account hold a session?" — the application's own rule, for example that a disabled or
/// locked-out account may not. The package asks at every sign-in and at every sliding renewal of a session cookie.
/// </summary>
/// <remarks>
/// <para>
/// At sign-in, "no" makes the sign-in throw <see cref="InvalidOperationException"/> before any session exists. At a
/// sliding renewal, "no" ends the session: the request is treated as unauthenticated and the cookie is cleared.
/// </para>
/// <para>
/// This is a backstop, not the revocation path: a session is only asked at renewal, which comes once more than
/// half of its lifetime has passed. Code that takes access away still calls <see cref="ISessionLifecycle"/>.
/// </para>
/// <para>
/// When the application registers no implementation, every account may hold a session.
/// </para>
/// </remarks>
public interface ISessionEligibility
{
    /// <summary>
    /// Decides whether the account may hold a session.
    /// </summary>
    /// <param name="accountId">The user account signing in or renewing its session.</param>
    /// <returns><see langword="true"/> when the account may hold a session.</returns>
    Task<bool> MayHoldSessionAsync(Guid accountId);
}
