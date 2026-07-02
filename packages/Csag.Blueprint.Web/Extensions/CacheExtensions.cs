namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Options.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for distributed cache registration.
/// </summary>
public static class CacheExtensions
{
    /// <summary>
    /// Adds IDistributedCache implementation based on configuration.
    /// Supports switching between Redis and SQL Server cache providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cacheOptions">The validated cache options.</param>
    /// <param name="configuration">The configuration instance for connection strings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConfigurableDistributedCache(
        this IServiceCollection services,
        CacheOptions cacheOptions,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(cacheOptions);
        ArgumentNullException.ThrowIfNull(configuration);

        if (cacheOptions.Provider == CacheProvider.Redis)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "SessionCache:";
            });
        }
        else
        {
            services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("Default connection string is required for SQL Server distributed cache");
                options.SchemaName = "dbo";
                options.TableName = "DistributedCache";
            });
        }

        // Add strongly-typed distributed cache wrapper with MessagePack serialization
        services.AddSerializedDistributedCache();

        return services;
    }
}
