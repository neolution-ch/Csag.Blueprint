namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Buffers.Text;
using System.Security.Cryptography;
using Csag.Blueprint.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

/// <summary>
/// Generates, hashes and verifies service-account secrets with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>
/// format, so every hash it ever wrote, and every hash an application wrote with a default
/// <see cref="PasswordHasher{TUser}"/>, keeps verifying.
/// </summary>
internal static class ServiceAccountSecretHasher
{
    // The hasher's defaults (PBKDF2 with HMAC-SHA512, 100,000 iterations, a 128-bit salt) are fixed here rather than
    // read from the application's PasswordHasherOptions, so a setting meant for user passwords cannot weaken
    // service-account secrets. Verification reads the parameters a hash was written with from the hash itself.
    private static readonly PasswordHasher<BlueprintServiceAccount> Hasher = new(Options.Create(new PasswordHasherOptions()));

    // PasswordHasher never reads the account it is handed; the parameter exists for hashers that salt per user.
    private static readonly BlueprintServiceAccount NoAccount = new();

    // Verified against when the client is unknown, so the expensive derivation runs for every request and the
    // response time does not tell whether a client exists. Written by the same hasher, so it costs the same.
    private static readonly Lazy<string> AbsentAccountHash = new(() => Hash(GenerateSecret()));

    /// <summary>
    /// Generates a secret: 256 random bits, base64url-encoded.
    /// </summary>
    /// <returns>The secret.</returns>
    public static string GenerateSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Hashes a secret.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The hash to store.</returns>
    public static string Hash(string secret) => Hasher.HashPassword(NoAccount, secret);

    /// <summary>
    /// Verifies a secret against a stored hash, taking the same time whether or not there is a hash to verify against.
    /// </summary>
    /// <param name="storedHash">The account's stored hash, or <see langword="null"/> when the client is unknown.</param>
    /// <param name="secret">The presented secret.</param>
    /// <returns><see langword="true"/> when the hash exists and the secret matches it.</returns>
    public static bool Verify(string? storedHash, string secret)
    {
        try
        {
            var result = Hasher.VerifyHashedPassword(NoAccount, storedHash ?? AbsentAccountHash.Value, secret);
            return storedHash is not null && result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // A stored value that is not a hash matches no secret. The derivation still runs, so a damaged row
            // answers in the time every other refusal does.
            Hasher.VerifyHashedPassword(NoAccount, AbsentAccountHash.Value, secret);
            return false;
        }
    }
}
