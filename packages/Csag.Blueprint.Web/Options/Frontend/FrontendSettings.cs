namespace Csag.Blueprint.Web.Options.Frontend
{
    /// <summary>
    /// General configuration settings for the frontend application.
    /// Holds cross-cutting frontend values (such as the base URL) that may be consumed by
    /// multiple backend features, for example when building links that are emailed to users.
    /// </summary>
    public sealed class FrontendSettings
    {
        /// <summary>
        /// Gets or sets the base URL (scheme and host only, no path) of the frontend application.
        /// Backend features append their own route paths and query parameters to this value.
        /// Example: "https://app.example.com".
        /// </summary>
        public string? BaseUrl { get; set; }
    }
}
