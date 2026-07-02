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

This registers the blueprint-owned validated options rooted under the `Blueprint` configuration section.

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

`UseBlueprintMiddleware()` applies the core blueprint request pipeline:

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
| `TenantMiddleware` | Sets ambient tenant context from authenticated claims. |
| `HttpAuditMiddleware` | Emits audit events for HTTP requests. |
| `CorrelationIdDelegatingHandler` | Propagates correlation IDs to outbound HTTP requests. |
| `SessionClaimRequestCultureProvider` | Resolves request culture from claims and `Accept-Language`. |
| `CultureNormalizationHelper` | Matches and validates requested cultures/languages. |
| `ReadyHealthCheck` | Reusable readiness gate used with startup orchestration. |

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

For the broader package architecture, see [`docs/architecture/PACKAGES.md`](../../docs/architecture/PACKAGES.md). For the current persistence and startup operating model, see [`docs/architecture/DATABASE.md`](../../docs/architecture/DATABASE.md).
