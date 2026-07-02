namespace Csag.Blueprint.Infrastructure.Localization;

using Csag.Blueprint.Infrastructure.Enums;
using Microsoft.Extensions.Caching.Memory;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Invalidates both the distributed cache (L2) and the in-process memory cache (L1)
/// for a given language's translations.
/// </summary>
public sealed class TranslationCacheInvalidator : ITranslationCacheInvalidator
{
    private readonly IDistributedCache<CacheId> distributedCache;
    private readonly IMemoryCache memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationCacheInvalidator"/> class.
    /// </summary>
    /// <param name="distributedCache">The distributed cache.</param>
    /// <param name="memoryCache">The in-process memory cache.</param>
    public TranslationCacheInvalidator(IDistributedCache<CacheId> distributedCache, IMemoryCache memoryCache)
    {
        this.distributedCache = distributedCache;
        this.memoryCache = memoryCache;
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(string languageCode, CancellationToken cancellationToken)
    {
        await this.distributedCache.RemoveAsync(CacheId.Translation, languageCode, cancellationToken);
        this.memoryCache.Remove($"translations:{languageCode}");
    }
}
