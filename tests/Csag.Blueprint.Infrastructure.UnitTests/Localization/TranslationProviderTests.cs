namespace Csag.Blueprint.Infrastructure.UnitTests.Localization;

using Csag.Blueprint.Application.Services;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Localization;
using Csag.Blueprint.Tests.Shared.Database;
using Csag.Blueprint.Tests.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Unit tests for <see cref="TranslationProvider{TContext}"/> using a real L1
/// <see cref="MemoryCache"/>, a mocked L2 distributed cache, and an in-memory DB context factory.
/// Covers the L1 → L2 → DB fallthrough order, the merge of DB rows over the default-language rows
/// and the code defaults, cache population on miss, and resilience against a failing L2.
/// </summary>
public sealed class TranslationProviderTests
{
    private const string DefaultLanguage = "en-GB";
    private const string RequestedLanguage = "de-CH";

    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["Key.A"] = "Default A",
        ["Key.B"] = "Default B",
        ["Key.C"] = "Default C",
    };

    [Fact]
    public void GetTranslations_L1Hit_ReturnsCachedSnapshotWithoutTouchingL2OrDb()
    {
        // Arrange — the L1 entry uses the "translations:{lang}" key the provider composes itself.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache<CacheId>>();
        var factory = new CountingDbContextFactory();
        var snapshot = new TranslationSnapshot { Translations = new Dictionary<string, string> { ["Key.A"] = "cached" } };
        memoryCache.Set($"translations:{RequestedLanguage}", snapshot);
        var provider = CreateProvider(memoryCache, l2.Object, factory);

        // Act
        var result = provider.GetTranslations(RequestedLanguage);

        // Assert
        result.ShouldBeSameAs(snapshot);
        l2.Verify(c => c.Get<TranslationSnapshot>(It.IsAny<CacheId>(), It.IsAny<string>()), Times.Never);
        factory.CreateCount.ShouldBe(0);
    }

    [Fact]
    public void GetTranslations_L2Hit_ReturnsSnapshotAndPopulatesL1()
    {
        // Arrange
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache<CacheId>>();
        var factory = new CountingDbContextFactory();
        var snapshot = new TranslationSnapshot { Translations = new Dictionary<string, string> { ["Key.A"] = "from L2" } };
        l2.Setup(c => c.Get<TranslationSnapshot>(CacheId.Translation, RequestedLanguage)).Returns(snapshot);
        var provider = CreateProvider(memoryCache, l2.Object, factory);

        // Act — two calls: the first fills L1 from L2, the second must be served from L1.
        var first = provider.GetTranslations(RequestedLanguage);
        var second = provider.GetTranslations(RequestedLanguage);

        // Assert
        first.ShouldBeSameAs(snapshot);
        second.ShouldBeSameAs(snapshot);
        l2.Verify(c => c.Get<TranslationSnapshot>(CacheId.Translation, RequestedLanguage), Times.Once);
        factory.CreateCount.ShouldBe(0);
    }

    [Fact]
    public void GetTranslations_CacheMiss_LoadsFromDbAndPopulatesBothCacheLayers()
    {
        // Arrange — both caches empty; the DB has a row overriding one code default.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache<CacheId>>();
        var factory = new CountingDbContextFactory();
        var updatedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        factory.Seed(CreateTranslation("Key.A", RequestedLanguage, "DB A", updatedAt));

        CacheEntryOptions? capturedOptions = null;
        l2
            .Setup(c => c.SetWithOptions(CacheId.Translation, RequestedLanguage, It.IsAny<TranslationSnapshot>(), It.IsAny<CacheEntryOptions>()))
            .Callback((CacheId _, string _, TranslationSnapshot _, CacheEntryOptions options) => capturedOptions = options);
        var provider = CreateProvider(memoryCache, l2.Object, factory);

        // Act
        var result = provider.GetTranslations(RequestedLanguage);
        var second = provider.GetTranslations(RequestedLanguage);

        // Assert — the DB row wins over the code default; unmapped keys keep their defaults.
        result.Translations["Key.A"].ShouldBe("DB A");
        result.Translations["Key.B"].ShouldBe("Default B");
        result.LastModified.ShouldBe(updatedAt);

        // L2 was populated with a 24h relative expiration, and L1 serves the second call (single DB hit).
        capturedOptions.ShouldNotBeNull().AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromHours(24));
        second.ShouldBeSameAs(result);
        factory.CreateCount.ShouldBe(1);
    }

    [Fact]
    public void GetTranslations_MergesRequestedLanguageOverDefaultLanguageOverCodeDefaults()
    {
        // Arrange — Key.A exists in both languages, Key.B only in the default language, and Key.C
        // exists in the requested language but with a NULL value (translation not provided yet).
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache<CacheId>>();
        var factory = new CountingDbContextFactory();
        factory.Seed(
            CreateTranslation("Key.A", RequestedLanguage, "de A", DateTimeOffset.UtcNow),
            CreateTranslation("Key.A", DefaultLanguage, "en A", DateTimeOffset.UtcNow),
            CreateTranslation("Key.B", DefaultLanguage, "en B", DateTimeOffset.UtcNow),
            CreateTranslation("Key.C", RequestedLanguage, value: null, DateTimeOffset.UtcNow));
        var provider = CreateProvider(memoryCache, l2.Object, factory);

        // Act
        var result = provider.GetTranslations(RequestedLanguage);

        // Assert — three-tier fallback per key: requested language → default language → code default.
        // A NULL DB value does not shadow the fallback tiers.
        result.Translations["Key.A"].ShouldBe("de A");
        result.Translations["Key.B"].ShouldBe("en B");
        result.Translations["Key.C"].ShouldBe("Default C");
    }

    [Fact]
    public void GetTranslations_L2Failing_FallsThroughToDbAndStillPopulatesL1()
    {
        // Arrange — a dead Redis must degrade to DB reads, not take localization down with it.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache<CacheId>>();
        var factory = new CountingDbContextFactory();
        factory.Seed(CreateTranslation("Key.A", RequestedLanguage, "DB A", DateTimeOffset.UtcNow));
        l2
            .Setup(c => c.Get<TranslationSnapshot>(It.IsAny<CacheId>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("cache unavailable"));
        l2
            .Setup(c => c.SetWithOptions(It.IsAny<CacheId>(), It.IsAny<string>(), It.IsAny<TranslationSnapshot>(), It.IsAny<CacheEntryOptions>()))
            .Throws(new InvalidOperationException("cache unavailable"));
        var provider = CreateProvider(memoryCache, l2.Object, factory);

        // Act
        var result = provider.GetTranslations(RequestedLanguage);
        var second = provider.GetTranslations(RequestedLanguage);

        // Assert — the snapshot comes from the DB and L1 still shields it from repeated DB loads.
        result.Translations["Key.A"].ShouldBe("DB A");
        second.ShouldBeSameAs(result);
        factory.CreateCount.ShouldBe(1);
    }

    private static TranslationProvider<TestDbContext> CreateProvider(
        IMemoryCache memoryCache,
        IDistributedCache<CacheId> l2,
        CountingDbContextFactory factory)
    {
        return new TranslationProvider<TestDbContext>(
            memoryCache,
            l2,
            factory,
            NullLogger<TranslationProvider<TestDbContext>>.Instance,
            DefaultLanguage,
            Defaults,
            l1ExpirationMinutes: 5);
    }

    private static BlueprintTranslation CreateTranslation(string key, string languageCode, string? value, DateTimeOffset updatedAt)
    {
        return new BlueprintTranslation
        {
            TranslationId = Guid.NewGuid(),
            Key = key,
            LanguageCode = languageCode,
            Value = value,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        };
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> over a single named in-memory database, counting
    /// context creations so tests can assert how often the provider fell through to the DB.
    /// </summary>
    internal sealed class CountingDbContextFactory : IDbContextFactory<TestDbContext>
    {
        private readonly DbContextOptions<TestDbContext> options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CountingDbContextFactory"/> class.
        /// </summary>
        public CountingDbContextFactory()
        {
            // The ambient tenant must be set so the model's tenant query filters can evaluate.
            TenantContext.SetTenant(TestDbContextFactory.TestTenantId);
            this.options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var context = new TestDbContext(this.options);
            context.Database.EnsureCreated();
        }

        /// <summary>
        /// Gets the number of contexts handed out through <see cref="CreateDbContext"/>.
        /// </summary>
        public int CreateCount { get; private set; }

        /// <inheritdoc/>
        public TestDbContext CreateDbContext()
        {
            this.CreateCount++;
            return new TestDbContext(this.options);
        }

        /// <summary>
        /// Inserts translation rows without affecting <see cref="CreateCount"/>.
        /// </summary>
        /// <param name="translations">The rows to insert.</param>
        public void Seed(params BlueprintTranslation[] translations)
        {
            using var context = new TestDbContext(this.options);
            context.Translations.AddRange(translations);
            context.SaveChanges();
        }
    }
}
