---
"@neolution-ch/csag-blueprint-web": minor
---

Narrow HTTP request auditing to denied requests only

**Breaking change.** `HttpAuditMiddleware` no longer records an event for every HTTP request. It
now records one event for a request whose final status code is 401 or 403 by default. GCP and
Application Insights already capture general request data. So a request outside the audited set
writes no event. The audit log now keeps only the EF Core events and the events for an audited HTTP
request.

An event keeps `EventType` (method and path), `StatusCode`, `UserId`, `TenantId`, `UserEmail`,
`UserDisplayName`, `CorrelationId`, `UserAgent`, and the new `IpAddress`. Every event drops
`HttpMethod`, `Url`, `DurationMs`, and `UserType`.

The audited status codes are configurable through `HttpAuditOptions.AuditedStatusCodes`, 401 and 403
by default. Register `services.Configure<HttpAuditOptions>(o => ...)` to audit a different set.

**Consumers must move the `app.UseMiddleware<HttpAuditMiddleware>()` call to before
`app.UseBlueprintMiddleware()`.** The ASP.NET Core authorization middleware does not call the next
middleware for a denied request. So `HttpAuditMiddleware` must wrap authentication and
authorization. It must not follow them. This release does not rename the type, so the compiler
does not flag a wrong position: an application that keeps the old registration still builds, but it
silently writes no HTTP audit events after this update.

That required position also places `HttpAuditMiddleware` outside the app's exception handler, which
`UseBlueprintMiddleware()` registers first. So the middleware now catches its own failure — a
resolver error or an audit-provider write failure — logs it, and lets the response continue instead
of turning it into an unhandled exception.
