namespace Csag.Blueprint.Web.Options;

using Csag.Blueprint.Web.Options.Api.Security;
using Csag.Blueprint.Web.Options.Cache;
using Csag.Blueprint.Web.Options.Database;
using Csag.Blueprint.Web.Options.FeatureFlags;
using Csag.Blueprint.Web.Options.Localization;

/// <summary>
/// Root configuration options for all Blueprint framework settings.
/// All Blueprint-related configuration is nested under the "Blueprint" section in appsettings.json.
/// </summary>
public sealed class BlueprintOptions
{
    /// <summary>
    /// Gets the configuration section name for Blueprint options.
    /// </summary>
    public static string SectionName => "Blueprint";

    /// <summary>
    /// Gets or sets the security configuration (CORS, HTTPS, JWT, OAuth, passwords, headers).
    /// </summary>
    public SecuritySettings Security { get; set; } = new();

    /// <summary>
    /// Gets or sets the distributed cache configuration.
    /// </summary>
    public CacheOptions Cache { get; set; } = new();

    /// <summary>
    /// Gets or sets the database management configuration.
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();

    /// <summary>
    /// Gets or sets the feature flag configuration.
    /// </summary>
    public FeatureFlagOptions FeatureFlags { get; set; } = new();

    /// <summary>
    /// Gets or sets the localization configuration.
    /// </summary>
    public LocalizationOptions Localization { get; set; } = new();
}
