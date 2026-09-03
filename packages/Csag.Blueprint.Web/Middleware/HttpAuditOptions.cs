namespace Csag.Blueprint.Web.Middleware;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Configuration for <see cref="HttpAuditMiddleware"/>.
/// </summary>
public sealed class HttpAuditOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether <see cref="HttpAuditMiddleware"/> records an event.
    /// The default is <see langword="false"/>. <c>UseBlueprintMiddleware()</c> always registers
    /// <see cref="HttpAuditMiddleware"/>, but it records nothing until an app calls
    /// <c>ConfigureBlueprintAuditLogging</c>, which sets this property to <see langword="true"/>
    /// before running its configure callback. That callback can set this back to
    /// <see langword="false"/> through <c>BlueprintAuditOptions.HttpAudit</c>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets the response status codes that <see cref="HttpAuditMiddleware"/> records an audit event
    /// for. The default holds 401 and 403. Add a code to also audit it, for example a custom
    /// rate-limit status. Remove a default code to stop auditing it.
    /// </summary>
    public ISet<int> AuditedStatusCodes { get; } = new HashSet<int>
    {
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden,
    };
}
