namespace Csag.Blueprint.Infrastructure.ServiceAccounts;

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text;
using Csag.Blueprint.Application.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Owns the service-account signing key and both uses of it: minting tokens and the parameters tokens are validated
/// against. Keeping the two together is what guarantees they derive the key the same way.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection.")]
internal sealed class ServiceAccountTokenSigning
{
    // The one algorithm tokens are signed with, and the only one validation accepts.
    private const string Algorithm = SecurityAlgorithms.HmacSha256;

    private readonly IOptions<BlueprintServiceAccountOptions> options;
    private readonly TimeProvider timeProvider;
    private readonly Lazy<SymmetricSecurityKey> signingKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAccountTokenSigning"/> class.
    /// </summary>
    /// <param name="options">The service-account settings.</param>
    /// <param name="timeProvider">The clock tokens are stamped and validated with.</param>
    public ServiceAccountTokenSigning(IOptions<BlueprintServiceAccountOptions> options, TimeProvider timeProvider)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        // Built on first use rather than here: a host exporting its OpenAPI document resolves the services that
        // depend on this one, but never issues or validates a token, and has no key to build.
        this.signingKey = new Lazy<SymmetricSecurityKey>(this.CreateSigningKey);
    }

    /// <summary>
    /// Mints a token for a session.
    /// </summary>
    /// <param name="clientId">The account's client id.</param>
    /// <param name="name">The account's display name.</param>
    /// <param name="sessionKey">The key of the session the token refers to.</param>
    /// <param name="issuedAt">When the token is issued.</param>
    /// <param name="expiresAt">When the token, and its session, expire.</param>
    /// <returns>The signed token.</returns>
    public string CreateToken(string clientId, string name, string sessionKey, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var settings = this.options.Value;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(this.signingKey.Value, Algorithm),
            Claims = new Dictionary<string, object>
            {
                [ServiceAccountClaimTypes.Subject] = clientId,
                [ServiceAccountClaimTypes.Name] = name,
                [ServiceAccountClaimTypes.Type] = ServiceAccountClaimTypes.ServiceAccountType,
                [ServiceAccountClaimTypes.SessionId] = sessionKey,
            },
        };

        // The handler would otherwise stamp the times above from the system clock.
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <summary>
    /// Creates the parameters the service-account scheme validates tokens against: the signing key and its one
    /// algorithm, the issuer, the audience, and an expiry checked against the injected clock with no skew.
    /// </summary>
    /// <returns>The parameters.</returns>
    public TokenValidationParameters CreateValidationParameters()
    {
        var settings = this.options.Value;
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = this.signingKey.Value,
            ValidAlgorithms = [Algorithm],
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,

            // IdentityModel compares lifetimes with the system clock; this reads the injected one instead, so token
            // expiry follows the same clock as the session it refers to. A token without an expiry is refused.
            LifetimeValidator = (notBefore, expires, _, _) => this.IsWithinLifetime(notBefore, expires),
            NameClaimType = ServiceAccountClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    }

    private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires)
    {
        if (expires is null)
        {
            return false;
        }

        var now = this.timeProvider.GetUtcNow().UtcDateTime;
        return (notBefore is null || notBefore.Value <= now) && now < expires.Value;
    }

    private SymmetricSecurityKey CreateSigningKey()
    {
        var key = this.options.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Service-account tokens need a signing key. Set Blueprint:Security:Jwt:SigningKey "
                + "(BlueprintServiceAccountOptions.SigningKey).");
        }

        if (BlueprintServiceAccountOptionsValidator.CheckSigningKey(key) is { } failure)
        {
            throw new InvalidOperationException(failure);
        }

        // UTF-8 over the key exactly as configured, for minting and validation alike.
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
