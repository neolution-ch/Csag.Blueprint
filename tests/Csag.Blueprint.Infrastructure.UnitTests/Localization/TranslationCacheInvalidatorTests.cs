namespace Csag.Blueprint.Infrastructure.UnitTests.Localization;

using Csag.Blueprint.Infrastructure.Enums;
using Csag.Blueprint.Infrastructure.Localization;
using Csag.Blueprint.Tests.Shared.Database;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Unit tests for <see cref="TranslationCacheInvalidator"/>. The invalidator and
/// <see cref="TranslationProvider{TContext}"/> share the L1 key format <c>translations:{lang}</c>
/// only by convention, so the eviction is exercised through a real provider populating a real
/// <see cref="MemoryCache"/> — a drifting key literal on either side would make this fail.
/// </summary>
public sealed class TranslationCacheInvalidatorTests
{
    [Fact]
    public async Task InvalidateAsync_RemovesBothCacheLayersForTheLanguage()
    {
        // Arrange — populate L1 for two languages through the provider (empty DB, empty L2).
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var l2 = new Mock<IDistributedCache<CacheId>>();
        var factory = new TranslationProviderTests.CountingDbContextFactory();
        var provider = CreateProvider(memoryCache, l2.Object, factory);
        provider.GetTranslations("de-CH");
        provider.GetTranslations("fr-CH");
        factory.CreateCount.ShouldBe(2);

        var invalidator = new TranslationCacheInvalidator(l2.Object, memoryCache);

        // Act
        await invalidator.InvalidateAsync("de-CH", TestContext.Current.CancellationToken);

        // Assert — L2 eviction is delegated to the distributed cache under the Translation cache id.
        l2.Verify(c => c.RemoveAsync(CacheId.Translation, "de-CH", It.IsAny<CancellationToken>()), Times.Once);

        // The invalidated language falls through to the DB again (L1 entry gone) while the other
        // language is still served from L1.
        provider.GetTranslations("de-CH");
        factory.CreateCount.ShouldBe(3);
        provider.GetTranslations("fr-CH");
        factory.CreateCount.ShouldBe(3);
    }

    private static TranslationProvider<TestDbContext> CreateProvider(
        IMemoryCache memoryCache,
        IDistributedCache<CacheId> l2,
        TranslationProviderTests.CountingDbContextFactory factory)
    {
        return new TranslationProvider<TestDbContext>(
            memoryCache,
            l2,
            factory,
            NullLogger<TranslationProvider<TestDbContext>>.Instance,
            "en-GB",
            new Dictionary<string, string> { ["Key.A"] = "Default A" },
            l1ExpirationMinutes: 5);
    }
}
