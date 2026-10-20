namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

/// <summary>
/// Fails startup on <see cref="BlueprintServiceAccountOptions"/> service-account credentials cannot run with.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class BlueprintServiceAccountOptionsValidator : IValidateOptions<BlueprintServiceAccountOptions>
{
    /// <summary>
    /// Checks a signing key's strength. Presence is not required here; see <see cref="BlueprintServiceAccountOptions.SigningKey"/>.
    /// </summary>
    /// <param name="signingKey">The configured key.</param>
    /// <returns>The failure message, or <see langword="null"/> when the key is absent or strong enough.</returns>
    public static string? CheckSigningKey(string? signingKey)
    {
        // Measured without surrounding white space, so padding cannot stand in for key material.
        if (signingKey is { Length: > 0 } && signingKey.Trim().Length < BlueprintServiceAccountOptions.MinimumSigningKeyLength)
        {
            return $"BlueprintServiceAccountOptions.SigningKey (Blueprint:Security:Jwt:SigningKey) must have at least "
                + $"{BlueprintServiceAccountOptions.MinimumSigningKeyLength} characters besides surrounding white space.";
        }

        return null;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BlueprintServiceAccountOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (CheckSigningKey(options.SigningKey) is { } signingKeyFailure)
        {
            failures.Add(signingKeyFailure);
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("BlueprintServiceAccountOptions.Issuer (Blueprint:Security:Jwt:Issuer) must not be blank.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("BlueprintServiceAccountOptions.Audience (Blueprint:Security:Jwt:Audience) must not be blank.");
        }

        // Token timestamps are whole seconds and the lifetime is reported to clients in whole seconds as an int, so a
        // shorter lifetime could issue a token that has already expired.
        if (options.TokenLifetime < TimeSpan.FromSeconds(1) || options.TokenLifetime.TotalSeconds > int.MaxValue)
        {
            failures.Add(
                "BlueprintServiceAccountOptions.TokenLifetime (Blueprint:Security:Jwt:ExpirationHours) must be at least one "
                + $"second and at most {int.MaxValue} seconds.");
        }

        if (options.MaxFailedAccessAttempts < 1)
        {
            failures.Add(
                "BlueprintServiceAccountOptions.MaxFailedAccessAttempts "
                + "(Blueprint:Security:ServiceAccountLockout:MaxFailedAccessAttempts) must be at least 1.");
        }

        if (options.LockoutDuration <= TimeSpan.Zero)
        {
            failures.Add(
                "BlueprintServiceAccountOptions.LockoutDuration "
                + "(Blueprint:Security:ServiceAccountLockout:LockoutDurationMinutes) must be positive.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
