# Csag.Blueprint.Application

## Overview

This package provides **application-layer abstractions** for CSAG Blueprint-based applications: service interfaces, claim type constants, tenant context management, and a full table view query system. It sits between Domain and Infrastructure, defining contracts that infrastructure and app layers implement.

## Claims

- **`IdentityClaimTypes`**: Static constants for identity claim types used across the application (`TenantId`, `Permission`, `PreferredLanguage`). They are stored in the authentication ticket so they are available on every request without a database lookup.
- **`ServiceAccountClaimTypes`**: The claim names a service-account bearer token and its principal carry — `Subject` (`sub`, the client id), `Name` (`name`), `Type` (`type`, always `ServiceAccountType`) and `SessionId` (`sid`, the opaque session reference). The bearer scheme does not map inbound claims, so these raw names are what the principal carries.

## Tenant Context

- **`TenantContext`**: Ambient `AsyncLocal`-backed storage for the current tenant ID. Set by middleware, read by query filters, save interceptors, and application code via `SetTenant()`, `Current`, and `Clear()`.

## Service Abstractions

All service contracts are defined as interfaces for dependency injection and testability.

| Interface                    | Purpose |
|------------------------------|---------|
| `ITenantService`             | Reads the current tenant ID from the authenticated user's claims. |
| `ISessionLifecycle`          | The application's entry point into the cookie session lifecycle. Seven intents — credentials changed, access revoked, claims changed, tenant removed, switch my session's tenant, list my sessions, revoke one of my sessions — decide which sessions end and which keep running with rebuilt claims. `RevokeMySessionAsync` returns `false` when the caller's account has no session with that id. No intent takes a `CancellationToken`: call it after the commit, and retry it on failure. |
| `ISessionRevocationParticipant` | Implemented by the application to end its own credentials (refresh tokens, API keys) whenever a revoking intent ends sessions. Every participant runs; failures are reported together. Each `SessionRevocation` names its `SessionSubject` — a user or a service account. |
| `ISessionEligibility`        | Implemented by the application to answer "may this account hold a session?", asked at every sign-in and sliding renewal. Without one, every account may. |
| `ITenantMembershipChecker`   | Answers whether a user is a member of a tenant without the tenant type parameters; the tenant manager implements it. |
| `IServiceAccountCredentials` | The application's entry point into service-account credentials: `IssueTokenAsync` (client id and secret to a `ServiceAccountTokenResult` — `Issued`, `InvalidCredentials` or `AccountInactive`), `CreateCredentialsAsync` and `RotateSecretAsync` (each returns the secret once as `ServiceAccountClientCredentials`). No `CancellationToken`. |
| `IServiceAccountSessionLifecycle` | The service-account counterpart of `ISessionLifecycle`: `AccountDeactivatedAsync` and `AccountDeletedAsync` end the account's sessions and tell the revocation participants, with a service-account `SessionSubject`. |
| `IServiceAccountClaimsEnricher` | Implemented by the application to add its own claims to a service-account principal on every request it authenticates. |
| `IPasswordResetEmailService` | Sends password reset emails containing a reset link. |
| `IImageStorageService`       | Stores and deletes images with support for multiple backends (database or blob storage). Returns a `StoredImageData` record with binary data or URL depending on the implementation. |

## Table View System

A generic, permission-aware system for server-side filtered, sorted, and paginated data grids. Application code defines views using a fluent API; infrastructure executes them against EF Core.

### Defining a table view

Subclass `TableViewDefinition<TEntity, TDto>` and configure columns in the constructor:

```csharp
public sealed class UserTableViewDefinition : TableViewDefinition<UserEntity, UserDto>
{
    public UserTableViewDefinition()
    {
        Column(dto => dto.Name)
            .Filterable(TableViewFilterOperator.Contains)
            .Sortable();

        Column(dto => dto.Email)
            .Filterable(TableViewFilterOperator.Contains)
            .Sortable();

        ComputedColumn("FullName", entity => entity.FirstName + " " + entity.LastName, dto => dto.FullName)
            .Sortable();
    }
}
```

### Key components

| Component | Purpose |
|-----------|---------|
| `TableViewDefinition<TEntity, TDto>` | Abstract base class with a fluent API for configuring columns, filters, sorting, and projection. |
| `TableViewColumnDefinition<TEntity, TDto>` | Fluent builder for a single column. Call `Filterable()`, `Sortable()`, and `WithDescription()` to configure. |
| `ITableViewDefinitionInfo` | Static metadata interface (`ViewId`, `DisplayName`, `RequiredPermission`) so infrastructure can discover endpoints without entity-specific code. |
| `ITableViewCatalogRegistration` | Registration contract for views in the catalog. Implementations are automatically discovered via DI. |
| `ITableViewCatalogService` | Discovers available table views filtered by user permissions. |
| `ITableViewExecutor` | Executes a `TableViewQueryRequest`: applies filters, counts, sorts, paginates, and projects to DTOs. |
| `ITableViewPreferencesService` | Persists per-user view preferences (column visibility, order, width, pinning, filters, sort, page size). |

### Filter operators

| Operator    | Description |
|-------------|-------------|
| `Equals`    | Exact match (`column = value`) |
| `Contains`  | Substring match (`column LIKE %value%`) |
| `Range`     | Numeric range (`column BETWEEN min AND max`) |
| `In`        | List match (`column IN (...)`) |
| `Boolean`   | True/false filter |
| `DateRange` | Date range filter |
| `Enum`      | Enum value filter with predefined allowed values |

### Allowed values and labels

`Enum` and `In` filters offer a column's `AllowedValues`, each a `TableViewAllowedValue` serialized as `{ "value": "...", "label": "..." }`. Filters match on `value`; `label` is display text.

An enum column derives one entry per member: the member name as the value, the humanized name as the label (`CreditCard` → "Credit card", `SMSCode` → "SMS code"), and `Enums.{EnumTypeName}.{MemberName}` as the label's translation key. The key is optional: add it to the translation catalog to translate the label, otherwise the humanized label is served.

```csharp
Column(dto => dto.PaymentMethod)
    .Filterable(TableViewFilterOperator.Enum)
    .WithValueLabelKeyPrefix("Payments.Methods"); // keys "Payments.Methods.{Value}", warned when missing

Column(dto => dto.Size)
    .Filterable(TableViewFilterOperator.In)
    .WithAllowedValues([new("S", "Small"), new("L", "Large") { LabelKey = "Sizes.Large" }]);
```

`WithAllowedValues(IEnumerable<string>)` and `Filterable(operator, allowedValues)` take plain strings, served with the string itself as the label. Excel exports write matching cells as their label.

### Request / Result models

- **`TableViewDataRequest`**: Incoming request with filters, sort, page, and page size.
- **`TableViewQueryRequest<TEntity, TDto>`**: Internal request that pairs a base `IQueryable` with a `ITableViewDefinition` and pagination parameters.
- **`TableViewDataResult<TDto>`**: Response with column metadata, data, total count, and computed page info.
- **`TableViewPreferencesModel`**: Serializable user preferences for a specific view.
