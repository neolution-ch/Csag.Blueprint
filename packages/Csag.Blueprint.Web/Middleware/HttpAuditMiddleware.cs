namespace Csag.Blueprint.Web.Middleware;

using Audit.Core;
using Csag.Blueprint.Web.Helpers;
using Csag.Blueprint.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// This middleware records an audit event for a request whose final status code is in
/// <see cref="HttpAuditOptions.AuditedStatusCodes"/>, 401 and 403 by default. A request outside
/// that set produces no event. The EF Core interceptor already audits the writes. GCP and
/// Application Insights already record general request data, for example the method, the URL, the
/// status code, and the duration. This audit log does not need that data.
/// </summary>
/// <remarks>
/// <c>app.UseBlueprintMiddleware()</c> always registers this middleware, first, so that it wraps
/// everything else in the pipeline: authentication and authorization, and also the app's exception
/// handler and status code pages. The ASP.NET Core authorization middleware does not call the next
/// middleware for a denied request, so a middleware registered after authorization never runs for a
/// denied request and never gets the chance to record an event. A middleware registered after the
/// exception handler never sees a status code that an unhandled exception produced, so it could
/// never audit one, for example a 500 added to <see cref="HttpAuditOptions.AuditedStatusCodes"/>.
/// An app does not need to register this middleware itself; remove any manual registration of it,
/// because a duplicate registration records two events per audited request.
/// <see cref="HttpAuditOptions.Enabled"/> gates the actual work; it stays
/// <see langword="false"/>, and this middleware records nothing, until an app calls
/// <c>ConfigureBlueprintAuditLogging</c>. An app can set
/// <c>BlueprintAuditOptions.HttpAudit.Enabled = false</c> in that call's configure callback to keep
/// HTTP request auditing off. Because this middleware sits outside the app's exception handler, an
/// unhandled exception here would not reach that handler; it would reach the ASP.NET Core default
/// error handling instead of the app's configured one. This middleware catches and logs its own
/// failure instead of throwing, so that a resolver or audit-provider failure produces neither
/// outcome: it stays a logged, best-effort miss of one audit event, and the already-decided status
/// code reaches the client unchanged.
/// </remarks>
public class HttpAuditMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<HttpAuditMiddleware> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpAuditMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public HttpAuditMiddleware(RequestDelegate next, ILogger<HttpAuditMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    /// <summary>
    /// This method processes an HTTP request. After the rest of the pipeline runs, it checks the
    /// final response status code. It records an audit event only when that code is in
    /// <see cref="HttpAuditOptions.AuditedStatusCodes"/>.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="tenantResolver">
    /// This middleware runs before <see cref="TenantMiddleware"/> (see the class remarks), so the
    /// ambient <see cref="Csag.Blueprint.Application.Services.TenantContext"/> is not set yet. This
    /// parameter resolves the tenant directly instead.
    /// </param>
    /// <param name="options">The audited status codes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver, IOptions<HttpAuditOptions> options)
    {
        if (!options.Value.Enabled)
        {
            await this.next(context);
            return;
        }

        var path = context.Request.Path.Value;
        if (IsExemptPath(path))
        {
            await this.next(context);
            return;
        }

        // Authorization denies a request by returning from this call without invoking the next
        // middleware. It does not throw. So a denial reaches the code below like any other request.
        await this.next(context);

        if (!options.Value.AuditedStatusCodes.Contains(context.Response.StatusCode))
        {
            return;
        }

        // The audited response may already carry the intended 401 or 403 status code. A resolver or
        // audit-provider failure must not overwrite that with a 500: it must stay a logged,
        // best-effort miss of one audit event. This code collects every field before it creates
        // the scope, because the default event creation policy saves the event on disposal. An
        // earlier scope creation risks a partial event, with only EventType set, before this
        // catch block runs.
        try
        {
            var eventType = $"HTTP:{context.Request.Method}:{context.Request.Path}";
            if (eventType.Length > 100)
            {
                eventType = eventType[..100];
            }

            var actor = AuditUserIdentity.FromPrincipal(context.User);
            var tenantId = await tenantResolver.ResolveAsync(context, context.RequestAborted);
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdKey, out var cid)
                ? cid?.ToString() : null;

            await using var scope = await AuditScope.CreateAsync(new AuditScopeOptions
            {
                EventType = eventType,
            });

            scope.SetCustomField("StatusCode", context.Response.StatusCode);
            scope.SetCustomField("UserId", actor.UserId);
            scope.SetCustomField("TenantId", tenantId);
            scope.SetCustomField("UserEmail", actor.Email);
            scope.SetCustomField("UserDisplayName", actor.DisplayName);
            scope.SetCustomField(CorrelationIdMiddleware.CorrelationIdKey, correlationId);
            scope.SetCustomField("UserAgent", context.Request.Headers.UserAgent.ToString());

            // ForwardedHeadersMiddleware (in UseBlueprintSecurityHeaders, registered before this
            // middleware) rewrites RemoteIpAddress from X-Forwarded-For, so this is the client's
            // address, not the address of a load balancer or a reverse proxy in front of it.
            scope.SetCustomField("IpAddress", context.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to record an HTTP audit event for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }

    /// <summary>
    /// Checks whether a path belongs to the health check tree or the Swagger tree. This method
    /// matches a full path segment, so it does not exempt an unrelated path that merely starts
    /// with the same characters, for example <c>/healthcare</c> or <c>/swagger-admin</c>.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns><see langword="true"/> if the path is exempt from auditing.</returns>
    private static bool IsExemptPath(string? path)
    {
        return IsPathUnder(path, "/health") || IsPathUnder(path, "/swagger");
    }

    private static bool IsPathUnder(string? path, string root)
    {
        if (path is null)
        {
            return false;
        }

        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
}
