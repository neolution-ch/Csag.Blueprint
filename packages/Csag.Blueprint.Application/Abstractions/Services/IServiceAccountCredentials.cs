namespace Csag.Blueprint.Application.Abstractions.Services;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// The application's entry point into service-account credentials: issuing bearer tokens for a client id and secret,
/// creating an account's credentials, and rotating its secret. The package owns the secret, its hash, the lockout
/// and the tokens; the application owns its endpoints and the validation of the account data it accepts.
/// </summary>
/// <remarks>
/// <para>
/// No method takes a <see cref="CancellationToken"/>: once called, an intent runs to completion, so a client that
/// disconnects cannot leave a secret half-rotated or a session issued without its token.
/// </para>
/// <para>
/// A plaintext secret leaves the package exactly once, in the result of <see cref="CreateCredentialsAsync"/> or
/// <see cref="RotateSecretAsync"/>; only its hash is stored.
/// </para>
/// <para>
/// Failures throw. A rotation that fails after storing the new secret still revokes and runs every
/// <see cref="ISessionRevocationParticipant"/>, then throws an <see cref="AggregateException"/>; calling it again
/// rotates to yet another secret, which is the one to hand out.
/// </para>
/// </remarks>
public interface IServiceAccountCredentials
{
    /// <summary>
    /// Issues a bearer token for a client id and secret. The secret is verified in the same time whether or not the
    /// client exists. A wrong secret counts towards the account's lockout; a locked account is refused whatever
    /// secret it is given. Each token is backed by a server-side session, so it carries no roles, permissions or
    /// tenant of its own.
    /// </summary>
    /// <param name="clientId">The client id the caller presented.</param>
    /// <param name="clientSecret">The secret the caller presented.</param>
    /// <returns>The token, or why none was issued.</returns>
    Task<ServiceAccountTokenResult> IssueTokenAsync(string clientId, string clientSecret);

    /// <summary>
    /// Creates a service account with fresh credentials: assigns <paramref name="serviceAccount"/> its id when it
    /// has none, a generated client id and the hash of a generated secret, then stores it. The account's name,
    /// roles, permissions, active flag and tenant are the caller's; the tenant comes from the ambient tenant context
    /// when the account is saved, as for every tenant-owned entity.
    /// </summary>
    /// <param name="serviceAccount">The new account, not yet stored.</param>
    /// <returns>The account's client id and its secret, which is not retrievable afterwards.</returns>
    Task<ServiceAccountClientCredentials> CreateCredentialsAsync(BlueprintServiceAccount serviceAccount);

    /// <summary>
    /// Replaces the account's secret with a generated one. The previous secret, and every token issued with it, stop
    /// working at once; there is no grace period. The account is looked up within the ambient tenant, as the
    /// application's own queries are.
    /// </summary>
    /// <param name="serviceAccountId">The account whose secret to rotate.</param>
    /// <returns>The account's client id and its new secret, or <see langword="null"/> when no such account exists in
    /// the ambient tenant.</returns>
    Task<ServiceAccountClientCredentials?> RotateSecretAsync(Guid serviceAccountId);
}
