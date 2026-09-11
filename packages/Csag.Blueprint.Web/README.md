# Csag.Blueprint.Web

## Overview

This package provides the **shared web-layer composition** for CSAG Blueprint-based applications.

It owns reusable validated options, builder and middleware composition helpers, FastEndpoints/Swagger setup, OAuth integration, correlation and request-culture infrastructure, and readiness support that applications can plug into their host.

## Core areas

### Options and validation

The package ships reusable option types and validators for:

- database startup behavior
- API/security settings
- cache provider settings
- feature flags
- localization settings

Use:

```csharp
services.AddBlueprintDefaultValidatedOptions(configuration);
```

This registers the package-owned validated options rooted under the `Blueprint` configuration section.

### Builder composition

`AddBlueprintServices(this WebApplicationBuilder)` wires shared web services such as:

- HTTPS redirection / HSTS / security headers setup
- CORS configuration
- Google OAuth authentication
- FastEndpoints registration
- Swagger/OpenAPI registration
- distributed cache registration
- anti-forgery services

### Middleware composition

The package provides two main composition helpers:

```csharp
app.UseBlueprintSecurityHeaders();
app.UseBlueprintMiddleware();
```

`UseBlueprintMiddleware()` applies the shared request pipeline:

- HTTP audit logging
- correlation ID middleware
- CORS
- authentication
- tenant middleware
- request localization
- authorization

Applications may still append app-specific middleware before endpoint mapping.

### Reusable middleware and services

| Component | Purpose |
| --- | --- |
| `CorrelationIdMiddleware` | Adds/propagates correlation IDs per request. |
| `TenantMiddleware` | Establishes the ambient tenant context for the request. Delegates *how* the tenant is determined to `ITenantResolver`. |
| `HttpAuditMiddleware` | Records an audit event for a denied request (401 or 403 by default; configurable). `UseBlueprintMiddleware()` always registers it; `ConfigureBlueprintAuditLogging` turns it on. See [Audit enrichment](#audit-enrichment). |
| `CorrelationIdDelegatingHandler` | Propagates correlation IDs to outbound HTTP requests. |
| `SessionClaimRequestCultureProvider` | Resolves request culture from claims and `Accept-Language`. |
| `CultureNormalizationHelper` | Matches and validates requested cultures/languages. |
| `StartupCompletedHealthCheck` | Reusable readiness gate used with startup orchestration. |

### Audit enrichment

`ConfigureBlueprintAuditLogging` adds the user ID, the email address, the display name, and the
correlation ID to each EF and HTTP audit event alike. It reads the three user values from the
claims on the request, not from the database. A service account has no email address, so its email
value is null and its display name is the account name from its token.

The provider writes `UserId`, `TenantId`, and `CorrelationId` to columns. The email address and the
display name stay in the `JsonData` column, at `$.UserEmail` and `$.UserDisplayName`. Reading either
value requires parsing the JSON data of the row.

`UseBlueprintMiddleware()` always registers `HttpAuditMiddleware`, first, so that it wraps
everything else in the pipeline: authentication and authorization, and also the exception handler
and status code pages. The ASP.NET Core authorization middleware does not call the next middleware
for a denied request, so a middleware registered later would never run for one. A middleware
registered after the exception handler would never see a status code that an unhandled exception
produced, so it could never audit one. An app does not need to register `HttpAuditMiddleware`
itself. Remove any manual `app.UseMiddleware<HttpAuditMiddleware>()` call; a duplicate registration
records two events per audited request.

`ConfigureBlueprintAuditLogging` configures both audit features, the EF Core events and the HTTP
request events, from this one call; both are on by default. Its configure callback exposes the same
`HttpAuditOptions` instance the middleware reads, as `BlueprintAuditOptions.HttpAudit`; set
`HttpAudit.Enabled = false` there to turn HTTP request auditing off.
Until `ConfigureBlueprintAuditLogging` runs, or when that flag is `false`, the middleware does
nothing but call the next middleware. Once on, it writes an event for a request whose final status
code is 401 or 403 by default. Call `services.Configure<HttpAuditOptions>(o => ...)` to audit a
different set. It also writes the address in
`HttpContext.Connection.RemoteIpAddress`, as resolved by `ForwardedHeadersMiddleware`. That is the
nearest hop, which is the caller only when nothing between the caller and this app appends its own
`X-Forwarded-For` entry — see the forwarded-headers note under Rate limiting below. A request outside the audited set writes no event: the EF Core interceptor
already covers the writes, and GCP and Application Insights already record general request data.

`HttpAuditMiddleware` wraps the app's exception handler rather than running inside it, so it can
also audit a status code the handler itself produces, for example a 500 added to
`HttpAuditOptions.AuditedStatusCodes`. It catches and logs its own failure instead of throwing,
because sitting outside the exception handler means an unhandled exception here would reach the
ASP.NET Core default error handling instead of the app's configured one.

### Tenant resolution (the addressing seam)

`ITenantResolver` decides which tenant an incoming request belongs to. The package ships
`ClaimsTenantResolver` as the default, which reads the tenant from the authenticated session's
`TenantId` claim — "session-resolved" addressing, where the tenant is a property of who you are
signed in as rather than of the URL you requested.

Other generic addressing strategies — a vanity subdomain (`acme.example.com`), a path segment
(`/t/acme`), a header-driven tenant — belong in this package as additional `ITenantResolver`
implementations; if the one you need is missing, add it here rather than in your application. A
custom, app-local implementation remains possible for an addressing scheme that is genuinely
app-specific and where no generic resolver makes sense. Either way the default is registered with
`TryAddScoped`, so a resolver registered before `AddBlueprintServices` wins and the package default
never has to be unregistered:

```csharp
builder.Services.AddScoped<ITenantResolver, MyAppSpecificTenantResolver>();
builder.AddBlueprintServices();
```

Two things to know before switching addressing strategy:

- `TenantMiddleware` runs **after** `UseAuthentication`/`UseAuthorization`, because the default
  resolver needs the authenticated principal. A host- or path-based resolver does not, and moving the
  middleware earlier is what enables per-tenant branding and per-tenant identity-provider routing on
  the sign-in page.
- Sign-in currently *derives* the tenant and writes it into the session ticket. Once the URL is the
  source of truth that relationship inverts, so session composition needs revisiting too. The resolver
  is the seam, not the whole job.

The resolver returns `Guid?`; `null` means "no tenant context", which is a normal state for anonymous
requests, platform-scope endpoints, and users who belong to no tenant.

### FastEndpoints and Swagger

The package owns:

- FastEndpoints registration helpers
- conventional endpoint routing/naming helpers
- Swagger/OpenAPI registration helpers

Applications still own their endpoint classes, DTOs, validators, and policies.

### Rate limiting

`RateLimiterExtensions` supplies the two pieces a host needs to wire up ASP.NET Core rate limiting;
the policies and their limits stay with the host.

`UseBlueprintRejectionResponse()` shapes the rejection: `429 Too Many Requests` instead of the
framework's `503 Service Unavailable` default (which an upstream load balancer reads as a backend
fault), an RFC 9457 ProblemDetails body carrying the correlation ID, and `Retry-After` when the
limiter reports it.

`TryGetClientPartitionKey(header, out key)` resolves the caller: the named edge-stamped header when
configured and parseable, else `Connection.RemoteIpAddress`, else nothing. It reports failure rather
than inventing a key, and a caller it cannot identify must be routed to
`RateLimitPartition.GetNoLimiter` — substituting a placeholder key puts every unidentifiable caller
in one bucket, so the first of them to exceed the limit rejects them all.

Prefer an edge-stamped header. `RemoteIpAddress` is the nearest hop, not the caller: `ForwardLimit 1`
reads the rightmost `X-Forwarded-For` entry, and an edge that appends its own — a Google external
load balancer sends `<client>,<balancer>` — leaves that edge's address rightmost. Behind a further
reverse proxy it is that proxy's egress address: one constant shared by every caller. Reading the
header from the left instead is worse, because the entries a client wrote are kept there unverified.
The header must be one the edge writes itself and overwrites inbound, so a client cannot forge it.

## Ownership Boundary

This package owns **reusable web composition**, not the application host itself.

The consuming application still owns:

- `Program.cs`
- endpoint implementations
- app-specific validators and option extensions
- host-specific runtime services
- concrete authentication/authorization decisions at the app level
