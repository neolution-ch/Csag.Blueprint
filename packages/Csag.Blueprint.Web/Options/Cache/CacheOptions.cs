namespace Csag.Blueprint.Web.Options.Cache
{
    /// <summary>
    /// Cache provider options.
    /// </summary>
    public enum CacheProvider
    {
        /// <summary>
        /// Use SQL Server as the distributed cache.
        /// </summary>
        SqlServer,

        /// <summary>
        /// Use Redis as the distributed cache.
        /// </summary>
        Redis,
    }

    /// <summary>
    /// Configuration options for distributed cache.
    /// Supports switching between SQL Server and Redis cache providers.
    /// </summary>
    public sealed class CacheOptions
    {
        /// <summary>
        /// Gets or sets the cache provider to use.
        /// </summary>
        public CacheProvider? Provider { get; set; }
    }
}
