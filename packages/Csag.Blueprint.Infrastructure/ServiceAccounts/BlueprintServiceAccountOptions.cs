namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

/// <summary>
/// Settings for service-account credentials, registered by
/// <see cref="ServiceCollectionExtensions.AddBlueprintServiceAccountAuthentication{TContext}"/>.
/// </summary>
/// <remarks>
/// <c>Csag.Blueprint.Web</c> sets every value from <c>Blueprint:Security:Jwt</c> and
/// <c>Blueprint:Security:ServiceAccountLockout</c> after every other configuration step, so those settings stay the
/// single source. Set them yourself only in a host that does not register the Web package's options. Startup fails
/// on an invalid value; see each property.
/// </remarks>
public sealed class BlueprintServiceAccountOptions
{
    /// <summary>
    /// The fewest characters a signing key may have once leading and trailing white space is removed, so that the
    /// HMAC-SHA256 key is at least 256 bits.
    /// </summary>
    public static readonly int MinimumSigningKeyLength = 32;

    /// <summary>
    /// Gets or sets the key tokens are signed and validated with (HMAC-SHA256 over its UTF-8 bytes). A key that is
    /// set must have at least <see cref="MinimumSigningKeyLength"/> characters besides surrounding white space,
    /// checked at startup. A missing key is accepted at startup, so a host started only to export its OpenAPI
    /// document needs none; issuing or validating a token without one fails.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// Gets or sets the issuer tokens are issued with and must carry. Must not be blank.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the audience tokens are issued for and must carry. Must not be blank.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets how long an issued token, and the session behind it, lasts. Must be at least one second.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets how many consecutive wrong secrets lock an account out. Must be at least 1.
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; }

    /// <summary>
    /// Gets or sets how long a lockout lasts. Must be positive.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; }
}
