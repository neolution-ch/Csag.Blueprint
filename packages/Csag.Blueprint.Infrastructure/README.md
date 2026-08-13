# Csag.Blueprint.Infrastructure

## Overview

This package provides the **shared infrastructure implementation layer** for CSAG Blueprint-based applications.

It contains the reusable EF Core persistence backbone, session/auth infrastructure, localization infrastructure, tenancy helpers, authorization transformation, and table-view execution components that consuming applications compose into their own host.

## What this package owns

### Persistence backbone

| Component | Purpose |
| --- | --- |
| `BlueprintDbContext<TAppTenant, TAppUser, TAppRole>` | Shared EF Core base context that owns the blueprint persistence model. |
| `Blueprint*Configuration` classes | EF Core mappings for blueprint-owned entities and inheritance roots. |
| `MultiTenancyModelBuilderExtensions` | Applies tenant filters/indexing/model conventions for tenant-scoped entities. See [Data isolation topology](#data-isolation-topology) for the database-per-tenant option. |

### Data isolation topology

`ConfigureBlueprintMultiTenancy` implements the **pooled** topology: every entity implementing
`IMustHaveTenant` gets a global query filter on `TenantId`, an index on that column, and a foreign key
to the tenant table.

```csharp
modelBuilder.ConfigureBlueprintMultiTenancy<ApplicationTenant, ApplicationDbContext>(this);
```

Because isolation is enforced by the *filter* and not the database, any table that cannot carry a
tenant discriminator — the user table, since identity is shared — is a permanent sharp edge that must
be scoped by membership on every query.

**Moving to database-per-tenant.** Pass `addTenantForeignKey: false` when tenant-owned data lives in a
different database from the tenant table; a foreign key cannot cross databases, so emitting one there
produces an invalid model. The filter and index still apply — only referential integrity moves from
the database to the application.

```csharp
modelBuilder.ConfigureBlueprintMultiTenancy<ApplicationTenant, BusinessDbContext>(
    this, addTenantForeignKey: false);
```

Note that `IMustHaveTenant` is **not** a synonym for "shardable". Some tenant-owned entities are
identity concerns — a service account is tenant-scoped but authentication needs it, so it must stay in
the central database. Classify each entity by plane before splitting anything.

### Interceptors

| Interceptor | Purpose |
| --- | --- |
| `AuditableTimestampInterceptor` | Sets `CreatedAt`/`UpdatedAt` automatically for `IAuditable` entities. |
| `TenantSaveInterceptor` | Assigns and protects `TenantId` for `IMustHaveTenant` entities. |

### Session and authorization infrastructure

| Component | Purpose |
| --- | --- |
| `DistributedCacheTicketStore` | ASP.NET Core `ITicketStore` implementation for server-side session storage. |
| `TicketCacheService` | Serialization/cache wrapper for authentication tickets. |
| `PostConfigureCookieAuthenticationOptions` | Injects the ticket store into cookie authentication options. |
| `SessionManager` | Shared session revocation/refresh management. |
| `PermissionClaimsTransformation` | Expands role claims into permission claims after authentication. |
| `UserManagerAuthorizationExtensions` | Loads roles and permissions for users. |

### Tenancy and localization infrastructure

| Component | Purpose |
| --- | --- |
| `TenantService` / `TenantManager` | Reusable tenant access and membership logic. |
| `BlueprintDbStringLocalizer` / `BlueprintDbStringLocalizerFactory` | Database-backed localization infrastructure. |
| `PassThroughStringLocalizer` | Generation-mode localizer that returns keys as-is. |
| `TranslationCacheKeys` | Cache key helpers for localization caching. |
| `MigrationBuilderExtensions` | Translation seeding helpers for migrations. |

### Table view infrastructure

| Component | Purpose |
| --- | --- |
| `TableViewExecutor` | Executes filtering, sorting, counting, pagination, and projection for table-view queries. |
| `TableViewCatalogService` | Discovers and filters registered table views by permission. |
| `BlueprintTableViewPreferencesService` | Persists per-user table view preferences. |

## Ownership Boundary

This package owns **reusable infrastructure**, not application composition.

The consuming application still owns:

- the concrete `ApplicationDbContext`
- DI composition and host setup
- app-specific options and policies
- app-specific entities and migrations

