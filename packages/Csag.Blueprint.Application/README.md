# Csag.Blueprint.Application

## Overview

This package provides **application-layer abstractions** for CSAG Blueprint-based applications: service interfaces, claim type constants, tenant context management, and a full table view query system. It sits between Domain and Infrastructure, defining contracts that infrastructure and app layers implement.

## Claims

- **`IdentityClaimTypes`**: Static constants for identity claim types used across the application (`TenantId`, `PreferredLanguage`). Stored in the authentication ticket so they are available on every request without a database lookup.

## Tenant Context

- **`TenantContext`**: Ambient `AsyncLocal`-backed storage for the current tenant ID. Set by middleware, read by query filters, save interceptors, and application code via `SetTenant()`, `Current`, and `Clear()`.

## Service Abstractions

All service contracts are defined as interfaces for dependency injection and testability.

| Interface                    | Purpose |
|------------------------------|---------|
| `ITenantService`             | Reads the current tenant ID from the authenticated user's claims. |
| `ISessionManager`            | Tracks, revokes, and refreshes authentication sessions. Supports per-user and per-session revocation, cleanup of expired records, and session refresh (role/permission updates). |
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

### Request / Result models

- **`TableViewDataRequest`**: Incoming request with filters, sort, page, and page size.
- **`TableViewQueryRequest<TEntity, TDto>`**: Internal request that pairs a base `IQueryable` with a `ITableViewDefinition` and pagination parameters.
- **`TableViewDataResult<TDto>`**: Response with column metadata, data, total count, and computed page info.
- **`TableViewPreferencesModel`**: Serializable user preferences for a specific view.
