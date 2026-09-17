namespace Csag.Blueprint.Infrastructure.UnitTests.Session;

using System.Security.Claims;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Moq;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Unit tests for <see cref="TicketCacheService"/>: the serialize/deserialize round-trip through the
/// distributed cache, the null/empty-payload guard on reads, and the propagation of the absolute
/// expiration to the cache entry options (the cache entry must die with the ticket, or a revoked
/// session's ticket would linger in the cache).
/// </summary>
public sealed class TicketCacheServiceTests
{
    private const string SessionKey = "test-session-key";

    [Fact]
    public async Task SetThenGetTicket_RoundTripsTicketThroughCache()
    {
        // Arrange — wire the mock so the bytes written by Set are the bytes returned by Get.
        var cache = new Mock<IDistributedCache<CacheId>>();
        byte[]? stored = null;
        cache
            .Setup(c => c.SetWithOptionsAsync(CacheId.AuthTicket, SessionKey, It.IsAny<byte[]>(), It.IsAny<CacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback((CacheId _, string _, byte[] bytes, CacheEntryOptions _, CancellationToken _) => stored = bytes)
            .Returns(Task.CompletedTask);
        cache
            .Setup(c => c.GetAsync<byte[]>(CacheId.AuthTicket, SessionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored!);
        var service = new TicketCacheService(cache.Object);

        // Whole seconds only: the ticket serializer stores properties with second precision.
        var expiresUtc = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var ticket = CreateTicket(expiresUtc);

        // Act
        await service.SetTicketAsync(SessionKey, ticket, expiresUtc, TestContext.Current.CancellationToken);
        var result = await service.GetTicketAsync(SessionKey, TestContext.Current.CancellationToken);

        // Assert — scheme, claims, and properties survive the round-trip.
        result.ShouldNotBeNull();
        result.AuthenticationScheme.ShouldBe("TestScheme");
        result.Principal.FindFirst(ClaimTypes.NameIdentifier).ShouldNotBeNull().Value.ShouldBe("user-1");
        result.Properties.ExpiresUtc.ShouldBe(expiresUtc);
        result.Properties.Items["custom"].ShouldBe("value");
    }

    [Fact]
    public async Task GetTicketAsync_MissingEntry_ReturnsNull()
    {
        // Arrange — a loose mock returns null bytes for an unknown key.
        var cache = new Mock<IDistributedCache<CacheId>>();
        var service = new TicketCacheService(cache.Object);

        // Act
        var result = await service.GetTicketAsync(SessionKey, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetTicketAsync_EmptyBytes_ReturnsNull()
    {
        // Arrange — an empty payload must be treated as a miss, not fed to the deserializer.
        var cache = new Mock<IDistributedCache<CacheId>>();
        cache
            .Setup(c => c.GetAsync<byte[]>(CacheId.AuthTicket, SessionKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = new TicketCacheService(cache.Object);

        // Act
        var result = await service.GetTicketAsync(SessionKey, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetTicketAsync_PropagatesAbsoluteExpirationToCacheEntryOptions()
    {
        // Arrange
        var cache = new Mock<IDistributedCache<CacheId>>();
        CacheEntryOptions? capturedOptions = null;
        cache
            .Setup(c => c.SetWithOptionsAsync(CacheId.AuthTicket, SessionKey, It.IsAny<byte[]>(), It.IsAny<CacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback((CacheId _, string _, byte[] _, CacheEntryOptions options, CancellationToken _) => capturedOptions = options)
            .Returns(Task.CompletedTask);
        var service = new TicketCacheService(cache.Object);

        var expiresUtc = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

        // Act
        await service.SetTicketAsync(SessionKey, CreateTicket(expiresUtc), expiresUtc, TestContext.Current.CancellationToken);

        // Assert — absolute expiration only; no sliding/relative expiration may creep in, because the
        // ticket's lifetime is authoritative.
        capturedOptions.ShouldNotBeNull();
        capturedOptions.AbsoluteExpiration.ShouldBe(expiresUtc);
        capturedOptions.AbsoluteExpirationRelativeToNow.ShouldBeNull();
        capturedOptions.SlidingExpiration.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveTicketAsync_RemovesUnderAuthTicketCacheId()
    {
        // Arrange
        var cache = new Mock<IDistributedCache<CacheId>>();
        var service = new TicketCacheService(cache.Object);

        // Act
        await service.RemoveTicketAsync(SessionKey, TestContext.Current.CancellationToken);

        // Assert
        cache.Verify(c => c.RemoveAsync(CacheId.AuthTicket, SessionKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_NullCache_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new TicketCacheService(null!));
    }

    private static AuthenticationTicket CreateTicket(DateTimeOffset expiresUtc)
    {
        var identity = new ClaimsIdentity("TestScheme");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        var properties = new AuthenticationProperties
        {
            ExpiresUtc = expiresUtc,
        };
        properties.Items["custom"] = "value";

        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, "TestScheme");
    }
}
