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
| `HttpAuditMiddleware` | Emits audit events for HTTP requests. |
| `CorrelationIdDelegatingHandler` | Propagates correlation IDs to outbound HTTP requests. |
| `SessionClaimRequestCultureProvider` | Resolves request culture from claims and `Accept-Language`. |
| `CultureNormalizationHelper` | Matches and validates requested cultures/languages. |
| `StartupCompletedHealthCheck` | Reusable readiness gate used with startup orchestration. |

### Audit enrichment

`ConfigureBlueprintAuditLogging` adds data about the user to each audit event. It adds the same data
to Entity Framework events and to HTTP events. The data is the user ID, the email address and the
display name of the user. The configuration also adds the correlation ID of the request.

The package reads the three user values from the claims on the request. It does not read them from
the database. Therefore an application can show the name of the user without a query on the user
table.

The claims are `ClaimTypes.NameIdentifier`, `ClaimTypes.Email` and `ClaimTypes.Name`. One helper
reads these claims for both write paths. A service account has no email address. Therefore its email
value is null, and its display name is the account name from the token.

The audit provider writes only `UserId` to a column. It writes the email address and the display name
to the `JsonData` column, at `$.UserEmail` and `$.UserDisplayName`. Audit.NET serializes custom fields
as JSON extension data.

This design does not change the schema. An application can install the new version without a
migration. But each read of these two values parses the JSON data of one row. If the reads are too
slow, add a column for each value with the `CustomColumn` mapping of the audit provider. That change
needs a migration.

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

## Ownership Boundary

This package owns **reusable web composition**, not the application host itself.

The consuming application still owns:

- `Program.cs`
- endpoint implementations
- app-specific validators and option extensions
- host-specific runtime services
- concrete authentication/authorization decisions at the app level
