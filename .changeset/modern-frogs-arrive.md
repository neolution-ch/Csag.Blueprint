---
"@neolution-ch/csag-blueprint-web": minor
---

Narrow HTTP request auditing to denied requests only, and register it automatically

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

**`app.UseBlueprintMiddleware()` now registers `HttpAuditMiddleware` itself, first, so that it always
wraps authentication and authorization.** An app must remove its own
`app.UseMiddleware<HttpAuditMiddleware>()` call. Keeping it registers the middleware twice, which
records two events per audited request.

`ConfigureBlueprintAuditLogging` now configures both audit features, the EF Core events and the HTTP
request events, from this one call. Both are on by default. Its configure callback can set
`BlueprintAuditOptions.HttpAudit.Enabled = false` to turn HTTP request auditing off.
