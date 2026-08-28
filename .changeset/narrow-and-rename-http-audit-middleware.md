---
"@neolution-ch/csag-blueprint-web": minor
---

Narrow HTTP request auditing to denied requests only, and rename the middleware

`HttpAuditMiddleware` is now `AuthorizationAuditMiddleware`. The old name no longer described its
behavior: it no longer records an event for every HTTP request. It now records one event for a
denied request (401 or 403). GCP and Application Insights already capture general request data. So
a successful request writes no event. The audit log now keeps only the EF Core events and the
events for a denied request.

An event for a denied request keeps `EventType` (method and path), `StatusCode`, `UserId`, `TenantId`,
`UserEmail`, `UserDisplayName`, `CorrelationId`, and `UserAgent`. Every event drops `HttpMethod`,
`Url`, `DurationMs`, and `UserType`.

**Consumers must replace `app.UseMiddleware<HttpAuditMiddleware>()` with
`app.UseMiddleware<AuthorizationAuditMiddleware>()`, and move the call to before
`app.UseBlueprintMiddleware()`.** The ASP.NET Core authorization middleware does not call the next
middleware for a denied request. So `AuthorizationAuditMiddleware` must wrap authentication and
authorization. It must not follow them. An application that keeps the old registration position
writes no HTTP audit events after this update.
