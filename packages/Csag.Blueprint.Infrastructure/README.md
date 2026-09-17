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
| `EntityFilteringModelBuilderExtensions` | Registers the named soft-delete query filter and creates the supporting `IX_{table}_DeletedAt` and composite `IX_{table}_ActiveRange` indexes for `ISoftDeletable` / `IHasActiveRange`. |
| `LocalizationModelBuilderExtensions` | Wires `IHasLocalizedTexts` entities to their `ILocalizedText` side, with the per-language unique constraint and indexes. |
| `ContractModelBuilderExtensions` | Applies the model conventions the domain contracts imply (`IHasInternalName` constraints, localized text constraints). |
| `KeyConventionModelBuilderExtensions` | Gives every single-column `Guid` primary key a `NEWSEQUENTIALID()` default. This applies to **all** entities, not only contract adopters. |

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

### Named global query filters

Both blueprint-owned global filters are registered under a name (`BlueprintQueryFilters.Tenant` and
`BlueprintQueryFilters.SoftDelete`) so a query can drop one without dropping the other. Parameterless
`IgnoreQueryFilters()` disables *every* filter on the entity, which would silently remove tenant
isolation — never use it to reach soft-deleted rows.

```csharp
// Reads deleted rows, still scoped to the current tenant.
var pedalo = await context.Pedalos
    .IgnoreSoftDeleteFilter()
    .FirstOrDefaultAsync(p => p.PedaloId == id, ct);

// Same, but restricted to the deleted rows only.
var deleted = await context.Pedalos.OnlySoftDeleted().ToListAsync(ct);
```

There is deliberately no convenience extension for opting out of the tenant filter: crossing tenants
must stay an explicit, reviewed `IgnoreQueryFilters(BlueprintQueryFilters.Tenant)` at the call site.

### When the conventions are applied

`BlueprintDbContext` does **not** apply the contract-driven conventions inside `OnModelCreating`. It
registers `BlueprintModelFinalizingConvention` in `ConfigureConventions`, which applies them once EF
Core has finished building the model.

That ordering matters. Applied inline, the conventions would only see the entity types discovered up
to that point, so anything your context registers *after* its `base.OnModelCreating(builder)` call —
the usual shape of `ApplyConfigurationsFromAssembly`, owned types and join entities, none of which
need a `DbSet` — would silently receive none of them. For `ISoftDeletable` that means no global query
filter at all, and soft-deleted rows coming back in every query. Running at finalization removes the
ordering requirement entirely: register entity types wherever you like.

A model convention is used rather than a replaced `IModelCustomizer` because registering that service
means modifying `DbContextOptions` from `OnConfiguring`, which EF Core forbids once `DbContext`
pooling is enabled:

```
'OnConfiguring' cannot be used to modify DbContextOptions when DbContext pooling is enabled.
```

Since `AddPooledDbContextFactory` is a normal way to register a context, that approach is not
available to a library. A convention touches neither options nor the service provider, so it works
under pooling.

If you override `ConfigureConventions`, **call `base.ConfigureConventions`** — that is where the
convention is registered, and without it none of the blueprint conventions, tenant isolation and
soft-delete filtering included, are applied.

Applications that do not derive from `BlueprintDbContext` can keep calling the `Configure*` extension
methods directly from their own `OnModelCreating` — they are unchanged, and remain the supported
entry point for that case. Call them last.

### Query extensions for the domain contracts

`EntityFilteringExtensions` and `LocalizationExtensions` provide the query-side counterparts to
the `Csag.Blueprint.Domain` contracts:

| Contract | Extensions |
| --- | --- |
| `ISoftDeletable` | `WhereNotDeleted()`, `WhereDeleted()`, `WhereDeletedBefore()` |
| `IHasActiveRange` | `WhereActiveNow()`, `WhereActiveAt()`, `WhereActiveToday()`, `WhereActiveInRange()`, `WhereInactiveNow()`, `WhereActiveAndNotDeleted()` |
| `IHasLocalizedTexts` / `ILocalizedText` | `SelectWithCurrentLanguageText()`, `CurrentLanguageTextExpression()`, `IncludeCurrentLanguageText()`, `WhereHasCurrentLanguageText()`, `GetCurrentLanguageText()`, `GetCurrentLanguageTextValue()` |

`WhereActiveNow()`, `WhereActiveAt()`, `WhereActiveInRange()` and `WhereInactiveNow()` compare against
an instant. `WhereActiveToday()` deliberately compares against **midnight of the current UTC day**, so
an entity whose range starts later today does **not** yet count as active — use `WhereActiveNow()` when
you need instant precision.

**`IHasActiveRange` is deliberately not a global query filter.** An active range is evaluated against
a point in time chosen by the caller, and "now" is only one of them — availability searches look at a
future window, and administrators legitimately need to see entities that are not active yet or no
longer active. Pick the point in time explicitly with the `WhereActive*` extensions.

Language resolution goes through `ICurrentLanguageProvider`; `DefaultLanguageProvider` reads
`CultureInfo.CurrentUICulture` (set per request by the ASP.NET Core request localization middleware)
and falls back to a configured language code when the culture is invariant. Use
`ExplicitLanguageProvider` where the language comes from the call itself (an endpoint parameter,
a user profile, a tenant setting), or register your own implementation in DI.

The fallback ranking is exact current language → same language in another region (including the bare
language code, so `de` matches a current language of `de-CH`) → exact fallback → same language as the
fallback, compared case-insensitively. A language that matches none of these four tiers is **never**
returned; the projection yields `null` instead of an arbitrary translation.

`SelectWithCurrentLanguageText()` applies that ranking inside a single EF Core query, so the
translation is resolved by the database rather than in memory:

```csharp
var cards = await context.Pedalos
    .WhereActiveNow()
    .SelectWithCurrentLanguageText<Pedalo, PedaloText, PedaloCard>(
        languageProvider,
        (pedalo, name) => new PedaloCard(pedalo.PedaloId, name ?? pedalo.InternalName))
    .ToListAsync(ct);
```

### Interceptors

| Interceptor | Purpose |
| --- | --- |
| `AuditableTimestampInterceptor` | Sets `CreatedAt`/`UpdatedAt` automatically for `IAuditable` entities. |
| `TenantSaveInterceptor` | Assigns and protects `TenantId` for `IMustHaveTenant` entities. |

Both are registered as singletons by `AddBlueprintTenancyRuntime()`. Resolve them from the container
when wiring the pooled `DbContext` factory rather than constructing them — they are singleton-safe
because they read ambient `AsyncLocal` state and a singleton `TimeProvider`, never scoped services.
`AuditableTimestampInterceptor` stamps from that `TimeProvider`, so a `FakeTimeProvider` registered
before `AddBlueprintTenancyRuntime()` controls the timestamps it writes.

### Session and authorization infrastructure

| Component | Purpose |
| --- | --- |
| `DistributedCacheTicketStore` | ASP.NET Core `ITicketStore` implementation for server-side session storage. |
| `TicketCacheService` | Serialization/cache wrapper for authentication tickets. |
| `PostConfigureCookieAuthenticationOptions` | Injects the ticket store into cookie authentication options. |
| `SessionManager` | Shared session revocation/refresh management. |
| `ServiceAccountSessionManager` | Tracks, validates, and revokes service-account sessions (cache marker plus tracking row) for reference-style JWTs. |
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

