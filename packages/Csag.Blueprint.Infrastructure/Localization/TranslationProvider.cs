namespace Csag.Blueprint.Infrastructure.Localization;

using System.Transactions;
using Csag.Blueprint.Domain.Entities;
using Csag.Blueprint.Infrastructure.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Neolution.Extensions.Caching.Abstractions;

/// <summary>
/// Provides merged translation snapshots with two-tier caching:
/// L1 (in-memory, short TTL) → L2 (distributed, 24h TTL) → Database.
/// </summary>
/// <typeparam name="TContext">The DbContext type that contains Translation entities.</typeparam>
public sealed class TranslationProvider<TContext> : ITranslationProvider
    where TContext : DbContext
{
    private const string L1CacheKeyPrefix = "translations:";

    private readonly IMemoryCache memoryCache;
    private readonly IDistributedCache<CacheId> distributedCache;
    private readonly IDbContextFactory<TContext> dbContextFactory;
    private readonly ILogger<TranslationProvider<TContext>> logger;
    private readonly string defaultLanguage;
    private readonly IReadOnlyDictionary<string, string> translationDefaults;
    private readonly TimeSpan l1ExpirationMinutes;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationProvider{TContext}"/> class.
    /// </summary>
    /// <param name="memoryCache">The in-memory cache (L1).</param>
    /// <param name="distributedCache">The distributed cache (L2).</param>
    /// <param name="dbContextFactory">The DB context factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="defaultLanguage">The default language code.</param>
    /// <param name="translationDefaults">Dictionary mapping dot-path keys to English default text.</param>
    /// <param name="l1ExpirationMinutes">The L1 cache expiration in minutes.</param>
    public TranslationProvider(
        IMemoryCache memoryCache,
        IDistributedCache<CacheId> distributedCache,
        IDbContextFactory<TContext> dbContextFactory,
        ILogger<TranslationProvider<TContext>> logger,
        string defaultLanguage,
        IReadOnlyDictionary<string, string> translationDefaults,
        int l1ExpirationMinutes)
    {
        this.memoryCache = memoryCache;
        this.distributedCache = distributedCache;
        this.dbContextFactory = dbContextFactory;
        this.logger = logger;
        this.defaultLanguage = defaultLanguage;
        this.translationDefaults = translationDefaults;
        this.l1ExpirationMinutes = TimeSpan.FromMinutes(l1ExpirationMinutes);
    }

    /// <inheritdoc/>
    public TranslationSnapshot GetTranslations(string languageCode)
    {
        var l1Key = $"{L1CacheKeyPrefix}{languageCode}";

        if (this.memoryCache.TryGetValue(l1Key, out TranslationSnapshot? cached) && cached != null)
        {
            return cached;
        }

        // Suppress any ambient transaction to avoid DTC escalation
        using var suppressTransaction = new TransactionScope(
            TransactionScopeOption.Suppress,
            TransactionScopeAsyncFlowOption.Enabled);

        // L2 check
        TranslationSnapshot? snapshot = null;
        try
        {
            snapshot = this.distributedCache.Get<TranslationSnapshot>(CacheId.Translation, languageCode);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to read translations from distributed cache for language '{LanguageCode}'", languageCode);
        }

        if (snapshot != null)
        {
            this.memoryCache.Set(l1Key, snapshot, this.l1ExpirationMinutes);
            return snapshot;
        }

        // DB load
        snapshot = this.LoadFromDatabase(languageCode);

        // Populate L2
        try
        {
            this.distributedCache.SetWithOptions(
                CacheId.Translation,
                languageCode,
                snapshot,
                new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to write translations to distributed cache for language '{LanguageCode}'", languageCode);
        }

        // Populate L1
        this.memoryCache.Set(l1Key, snapshot, this.l1ExpirationMinutes);

        return snapshot;
    }

    private TranslationSnapshot LoadFromDatabase(string languageCode)
    {
        using var context = this.dbContextFactory.CreateDbContext();

        // Load translations for the requested language
        var translationData = context.Set<BlueprintTranslation>()
            .AsNoTracking()
            .Where(t => t.LanguageCode == languageCode)
            .Select(t => new { t.Key, t.Value, t.UpdatedAt })
            .ToList();

        var translations = translationData.ToDictionary(t => t.Key, t => t.Value);
        var lastModified = translationData.Count > 0 ? translationData.Max(t => t.UpdatedAt) : (DateTimeOffset?)null;

        // Load default language translations for fallback (if different)
        Dictionary<string, string?>? defaultTranslations = null;
        DateTimeOffset? defaultLastModified = null;
        if (!string.Equals(languageCode, this.defaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var defaultData = context.Set<BlueprintTranslation>()
                .AsNoTracking()
                .Where(t => t.LanguageCode == this.defaultLanguage)
                .Select(t => new { t.Key, t.Value, t.UpdatedAt })
                .ToList();

            defaultTranslations = defaultData.ToDictionary(t => t.Key, t => t.Value);
            defaultLastModified = defaultData.Count > 0 ? defaultData.Max(t => t.UpdatedAt) : null;
        }

        // Determine the overall last modified timestamp
        var overallLastModified = new[] { lastModified, defaultLastModified }.Max();

        // Build merged dictionary with 3-tier fallback
        var merged = new Dictionary<string, string>();
        foreach (var kvp in this.translationDefaults)
        {
            var key = kvp.Key;
            var codeDefault = kvp.Value;

            if (translations.TryGetValue(key, out var value) && value != null)
            {
                merged[key] = value;
            }
            else if (defaultTranslations != null && defaultTranslations.TryGetValue(key, out var defaultValue) && defaultValue != null)
            {
                merged[key] = defaultValue;
            }
            else
            {
                merged[key] = codeDefault;
            }
        }

        return new TranslationSnapshot
        {
            Translations = merged,
            LastModified = overallLastModified,
        };
    }
}
