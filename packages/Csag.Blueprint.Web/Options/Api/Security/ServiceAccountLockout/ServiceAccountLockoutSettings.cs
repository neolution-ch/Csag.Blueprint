namespace Csag.Blueprint.Web.Options.Api.Security.ServiceAccountLockout
{
    /// <summary>
    /// Configuration settings for the service-account token endpoint lockout / throttle policy.
    /// Mirrors the semantics of ASP.NET Core Identity's lockout (used for interactive user login), but applies
    /// to <c>BlueprintServiceAccount</c> credentials, which are not Identity users and therefore do not benefit
    /// from Identity's built-in lockout.
    /// </summary>
    public sealed class ServiceAccountLockoutSettings
    {
        /// <summary>
        /// Gets or sets the number of consecutive failed authentication attempts that triggers a lockout.
        /// Once the failed-attempt count reaches this value the account is locked for
        /// <see cref="LockoutDurationMinutes"/> minutes.
        /// </summary>
        public int MaxFailedAccessAttempts { get; set; }

        /// <summary>
        /// Gets or sets the duration, in minutes, for which a service account remains locked out after the
        /// failed-attempt threshold is reached.
        /// </summary>
        public int LockoutDurationMinutes { get; set; }
    }
}
