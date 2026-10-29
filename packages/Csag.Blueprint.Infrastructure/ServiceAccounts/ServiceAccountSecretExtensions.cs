namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// Sets a known secret on a service account, for data seeds and test fixtures that need to authenticate with a secret
/// they chose. Production code creates and rotates secrets through <c>IServiceAccountCredentials</c>, which generates
/// them.
/// </summary>
public static class ServiceAccountSecretExtensions
{
    /// <summary>
    /// Stores the hash of <paramref name="clientSecret"/> on the account, in the format the package verifies. Saving
    /// the account is the caller's.
    /// </summary>
    /// <param name="serviceAccount">The account.</param>
    /// <param name="clientSecret">The secret the account should accept.</param>
    public static void SetClientSecret(this BlueprintServiceAccount serviceAccount, string clientSecret)
    {
        ArgumentNullException.ThrowIfNull(serviceAccount);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        serviceAccount.ClientSecretHash = ServiceAccountSecretHasher.Hash(clientSecret);
    }
}
