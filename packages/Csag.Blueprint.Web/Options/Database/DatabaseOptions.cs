namespace Csag.Blueprint.Web.Options.Database
{
    /// <summary>
    /// Configuration options for database management and startup behavior.
    /// </summary>
    public sealed class DatabaseOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether gets or sets whether database migrations should be automatically applied at application startup.
        /// When true, pending migrations are automatically applied before the application starts accepting requests.
        /// When false (recommended for production), the application will fail to start if pending migrations are detected.
        /// </summary>
        public bool ApplyMigrationsAutomaticallyDuringStartup { get; set; }
    }
}
