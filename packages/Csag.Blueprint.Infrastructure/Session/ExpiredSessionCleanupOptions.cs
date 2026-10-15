namespace Csag.Blueprint.Infrastructure.Session;

/// <summary>
/// Settings of the background job that deletes the tracking rows of expired sessions. See
/// <see cref="ExpiredSessionCleanupService{TContext}"/>.
/// </summary>
public sealed class ExpiredSessionCleanupOptions
{
    /// <summary>
    /// The longest interval the job accepts. A pass deletes only rows that have already expired, so running less
    /// often than daily lets the table grow without making anything safer.
    /// </summary>
    public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets a value indicating whether the job runs. Defaults to <see langword="true"/>. Turn it off only
    /// when something else deletes expired rows from <c>BlueprintActiveSessions</c> and
    /// <c>BlueprintServiceAccountSessions</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the time between two passes. Defaults to one hour. The first pass runs one interval after the
    /// host starts. Must be positive and at most <see cref="MaxInterval"/>.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}
