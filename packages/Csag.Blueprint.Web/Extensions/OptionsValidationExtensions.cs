namespace Csag.Blueprint.Web.Extensions;

using Csag.Blueprint.Web.Options;
using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Cache;
using Csag.Blueprint.Web.Options.Database;
using Csag.Blueprint.Web.Options.Localization;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Extension methods for validating configuration options from IConfiguration.
/// </summary>
public static class OptionsValidationExtensions
{
    /// <summary>
    /// Gets and validates SecuritySettings from configuration.
    /// Binds SecuritySettings from the Blueprint:Security section and validates them synchronously.
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The validated SecuritySettings instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when SecuritySettings validation fails.</exception>
    public static SecuritySettings GetValidatedSecuritySettings(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var securitySettings = configuration.GetSection($"{BlueprintOptions.SectionName}:Security").Get<SecuritySettings>()!;

        var validator = new SecuritySettingsValidator();
#pragma warning disable CA1849 // Synchronous validation is acceptable during startup configuration
        var validationResult = validator.Validate(securitySettings);
#pragma warning restore CA1849
        if (!validationResult.IsValid)
        {
            var errors = string.Join(Environment.NewLine, validationResult.Errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException($"{BlueprintOptions.SectionName}:Security {nameof(configuration)} validation failed:{Environment.NewLine}{errors}");
        }

        return securitySettings;
    }

    /// <summary>
    /// Validates and returns the CacheOptions from configuration before the application is built.
    /// This is necessary because cache configuration is needed during the builder phase (before app.Build()).
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The validated CacheOptions instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when CacheOptions validation fails.</exception>
    public static CacheOptions GetValidatedCacheOptions(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var cacheOptions = configuration.GetSection($"{BlueprintOptions.SectionName}:Cache").Get<CacheOptions>()!;

        var validator = new CacheOptionsValidator(configuration);
#pragma warning disable CA1849 // Synchronous validation is acceptable during startup configuration
        var validationResult = validator.Validate(cacheOptions);
#pragma warning restore CA1849
        if (!validationResult.IsValid)
        {
            var errors = string.Join(Environment.NewLine, validationResult.Errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException($"{BlueprintOptions.SectionName}:Cache {nameof(configuration)} validation failed:{Environment.NewLine}{errors}");
        }

        return cacheOptions;
    }

    /// <summary>
    /// Validates and returns the LocalizationOptions from configuration before the application is built.
    /// This is necessary because localization configuration is needed during the builder phase (before app.Build()).
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The validated LocalizationOptions instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when LocalizationOptions validation fails.</exception>
    public static LocalizationOptions GetValidatedLocalizationOptions(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var localizationOptions = configuration.GetSection($"{BlueprintOptions.SectionName}:Localization").Get<LocalizationOptions>()!;

        var validator = new LocalizationOptionsValidator();
#pragma warning disable CA1849 // Synchronous validation is acceptable during startup configuration
        var validationResult = validator.Validate(localizationOptions);
#pragma warning restore CA1849
        if (!validationResult.IsValid)
        {
            var errors = string.Join(Environment.NewLine, validationResult.Errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException($"{BlueprintOptions.SectionName}:Localization {nameof(configuration)} validation failed:{Environment.NewLine}{errors}");
        }

        return localizationOptions;
    }

    /// <summary>
    /// Validates and returns the DatabaseOptions from configuration, ensuring required connection strings are configured.
    /// This is necessary to validate that migration connection string is present when auto-apply migrations is enabled.
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The validated DatabaseOptions instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when DatabaseOptions validation fails or required connection strings are missing.</exception>
    public static DatabaseOptions GetValidatedDatabaseOptions(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseOptions = configuration.GetSection($"{BlueprintOptions.SectionName}:Database").Get<DatabaseOptions>()!;

        var validator = new DatabaseOptionsValidator();
#pragma warning disable CA1849 // Synchronous validation is acceptable during startup configuration
        var validationResult = validator.Validate(databaseOptions);
#pragma warning restore CA1849
        if (!validationResult.IsValid)
        {
            var errors = string.Join(Environment.NewLine, validationResult.Errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException($"{BlueprintOptions.SectionName}:Database {nameof(configuration)} validation failed:{Environment.NewLine}{errors}");
        }

        // Validate required connection strings
        var runtimeConnectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(runtimeConnectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is required but not configured.");
        }

        if (databaseOptions.ApplyMigrationsAutomaticallyDuringStartup)
        {
            var migrationConnectionString = configuration.GetConnectionString("Migrations");
            if (string.IsNullOrWhiteSpace(migrationConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Migrations is required when ApplyMigrationsAutomaticallyDuringStartup is enabled. " +
                    "The migration connection string must have elevated privileges (e.g., database owner or sysadmin) to apply schema changes.");
            }
        }

        return databaseOptions;
    }
}
