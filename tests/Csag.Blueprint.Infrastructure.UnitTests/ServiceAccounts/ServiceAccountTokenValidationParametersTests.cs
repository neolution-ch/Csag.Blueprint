namespace Csag.Blueprint.Infrastructure.UnitTests.ServiceAccounts;

using System.Text;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Unit tests for the parameters the service-account bearer scheme validates tokens against, driven with tokens minted
/// here: the pinned algorithm, issuer and audience, an expiry read from the injected clock, and the key derived from
/// the configured string's UTF-8 bytes. What happens after these checks — the session — is covered by the integration
/// tests.
/// </summary>
public sealed class ServiceAccountTokenValidationParametersTests
{
    [Fact]
    public async Task AWellFormedToken_IsValidAgainstTheInjectedClockAsync()
    {
        // Arrange — the token's lifetime lies years before the wall clock, inside the fake clock's.
        using var services = ServiceAccountTestServices.Build();
        var token = Mint(ServiceAccountTestServices.SigningKey);

        // Act
        var result = await ValidateAsync(services, token);

        // Assert
        result.IsValid.ShouldBeTrue(result.Exception?.Message);
    }

    [Fact]
    public async Task ATokenAtItsExpiryOnTheInjectedClock_IsRefusedAsync()
    {
        var clock = new FakeTimeProvider(ServiceAccountTestServices.ClockStart);
        using var services = ServiceAccountTestServices.Build(clock);
        var token = Mint(ServiceAccountTestServices.SigningKey);

        clock.Advance(TimeSpan.FromHours(1));
        var result = await ValidateAsync(services, token);

        result.IsValid.ShouldBeFalse();
        result.Exception.ShouldBeAssignableTo<SecurityTokenInvalidLifetimeException>();
    }

    [Fact]
    public async Task ATokenWithoutAnExpiry_IsRefusedAsync()
    {
        using var services = ServiceAccountTestServices.Build();
        var token = Mint(ServiceAccountTestServices.SigningKey, descriptor => descriptor.Expires = null);

        (await ValidateAsync(services, token)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(SecurityAlgorithms.HmacSha384)]
    [InlineData(SecurityAlgorithms.HmacSha512)]
    public async Task ATokenSignedWithTheRightKeyButAnotherAlgorithm_IsRefusedAsync(string algorithm)
    {
        // A key long enough for every HMAC variant, so the token can be signed with each of them.
        var signingKey = new string('k', 64);
        using var services = ServiceAccountTestServices.Build(configure: o => o.SigningKey = signingKey);
        var token = Mint(signingKey, algorithm: algorithm);
        (await ValidateAsync(services, Mint(signingKey))).IsValid.ShouldBeTrue("the same token signed with HS256 is accepted");

        // The signature itself is sound for that algorithm and key, so the algorithm is the only thing refused.
        (await ValidateAsync(services, token)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnsignedToken_IsRefusedAsync()
    {
        using var services = ServiceAccountTestServices.Build();
        var token = Mint(ServiceAccountTestServices.SigningKey, descriptor => descriptor.SigningCredentials = null);

        (await ValidateAsync(services, token)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task ATokenForAnotherIssuerOrAudience_IsRefusedAsync()
    {
        using var services = ServiceAccountTestServices.Build();
        var otherIssuer = Mint(ServiceAccountTestServices.SigningKey, descriptor => descriptor.Issuer = "someone-else");
        var otherAudience = Mint(ServiceAccountTestServices.SigningKey, descriptor => descriptor.Audience = "someone-else");

        (await ValidateAsync(services, otherIssuer)).Exception.ShouldBeAssignableTo<SecurityTokenInvalidIssuerException>();
        (await ValidateAsync(services, otherAudience)).Exception.ShouldBeAssignableTo<SecurityTokenInvalidAudienceException>();
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRefusedAsync()
    {
        using var services = ServiceAccountTestServices.Build();
        var token = Mint("AnotherSigningKeyThatIsAtLeast32CharactersLong!!");

        (await ValidateAsync(services, token)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task TheKeyIsTheConfiguredStringsUtf8BytesAsync()
    {
        // Arrange — a key whose UTF-8 and ASCII encodings differ. Signing and validation must agree on one encoding,
        // and the package uses UTF-8 on both sides, so only the UTF-8 signature validates.
        var signingKey = "Schlüssel-mit-Umlauten-äöü-der-lang-genug-ist";
        using var services = ServiceAccountTestServices.Build(configure: o => o.SigningKey = signingKey);

        // Act
        var overUtf8 = await ValidateAsync(services, Mint(Encoding.UTF8.GetBytes(signingKey)));
        var overAscii = await ValidateAsync(services, Mint(Encoding.ASCII.GetBytes(signingKey)));

        // Assert
        overUtf8.IsValid.ShouldBeTrue(overUtf8.Exception?.Message);
        overAscii.IsValid.ShouldBeFalse();
    }

    private static Task<TokenValidationResult> ValidateAsync(IServiceProvider services, string token)
        => new JsonWebTokenHandler().ValidateTokenAsync(token, services.GetBearerOptions().TokenValidationParameters);

    private static string Mint(string signingKey, Action<SecurityTokenDescriptor>? change = null, string algorithm = SecurityAlgorithms.HmacSha256)
        => Mint(Encoding.UTF8.GetBytes(signingKey), change, algorithm);

    private static string Mint(byte[] keyBytes, Action<SecurityTokenDescriptor>? change = null, string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var issuedAt = ServiceAccountTestServices.ClockStart.UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = ServiceAccountTestServices.Issuer,
            Audience = ServiceAccountTestServices.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), algorithm),
            Claims = new Dictionary<string, object> { ["sub"] = "sa-unit", ["sid"] = "session" },
        };
        change?.Invoke(descriptor);

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }
}
