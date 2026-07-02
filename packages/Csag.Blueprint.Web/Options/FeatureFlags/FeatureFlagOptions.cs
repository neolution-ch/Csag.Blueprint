namespace Csag.Blueprint.Web.Options.FeatureFlags
{
    /// <summary>
    /// Configuration options for feature flags.
    /// </summary>
    public sealed class FeatureFlagOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the configuration-based feature flag example endpoint is enabled.
        /// </summary>
        public bool EnableConfigurationExample { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the simulate error test endpoints are enabled.
        /// </summary>
        public bool EnableSimulateError { get; set; }
    }
}
