namespace Csag.Blueprint.Web.Middleware;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Configuration for <see cref="HttpAuditMiddleware"/>.
/// </summary>
public sealed class HttpAuditOptions
{
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
