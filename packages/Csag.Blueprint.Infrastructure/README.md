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

Both are registered as singletons by `AddBlueprintTenancyRuntime()`. Resolve them from the container
when wiring the pooled `DbContext` factory rather than constructing them — they are singleton-safe
because they read ambient `AsyncLocal` state and a singleton `TimeProvider`, never scoped services.
`AuditableTimestampInterceptor` stamps from that `TimeProvider`, so a `FakeTimeProvider` registered
before `AddBlueprintTenancyRuntime()` controls the timestamps it writes.

### Session and authorization infrastructure

| Component | Purpose |
| --- | --- |
| `DistributedCacheTicketStore` | ASP.NET Core `ITicketStore` implementation for server-side session storage. Owns session tracking: every ticket it stores or renews is mirrored into the session's tracking row. |
| `ActiveSessionTracker<TContext>` | Writes the tracking rows (`BlueprintActiveSessions`) for the ticket store: create or re-attribute on sign-in, follow the ticket on renewal, delete on removal. |
| `TicketCacheService` | Serialization/cache wrapper for authentication tickets. |
| `PostConfigureCookieAuthenticationOptions` | Injects the ticket store into cookie authentication options. |
| `ISessionLifecycle` implementation | The intents applications call; registered by `AddBlueprintSessionInfrastructure`. |
| `ExpiredSessionCleanupService<TContext>` | Hosted job deleting the tracking rows of expired sessions, user and service-account sessions alike. |
| `IServiceAccountCredentials` / `IServiceAccountSessionLifecycle` implementations | The service-account intents; registered by `AddBlueprintServiceAccountAuthentication`. See [Service-account credentials](#service-account-credentials). |
| `PermissionClaimsTransformation` | Expands role claims into permission claims after authentication. |
| `UserManagerAuthorizationExtensions` | Loads roles and permissions for users. |

### Cookie sessions

The packages own the cookie session lifecycle; the application registers it and states intents.

```csharp
services.AddBlueprintSessionInfrastructure<ApplicationUser, ApplicationDbContext>(options =>
    options.ClientIpHeader = "X-Client-IP");
services.AddBlueprintCookieSessions(cookie =>
{
    cookie.SecurePolicy = securitySettings.CookieSecurePolicy;
    cookie.SameSite = SameSiteMode.Lax;
});
```

`AddBlueprintCookieSessions` turns the Identity application cookie into the session cookie, after every other
`Configure` (including `AddIdentity`'s) in either registration order. The callback shapes the cookie; the package then
applies what it owns:

- the ticket store, with the session tracked server-side and the cookie carrying only its key;
- an HTTP-only, persistent cookie whose lifetime is `BlueprintSessionOptions.Lifetime` — set from
  `Blueprint:Security:SessionExpirationHours` by `Csag.Blueprint.Web` — with sliding expiration, so sign-in code sets
  no expiry;
- 401 and 403 instead of login and access-denied redirects;
- no Identity security-stamp validation: the session principal carries no stamp, and sessions end through the
  lifecycle instead;
- `ISessionEligibility` asked at every sign-in and sliding renewal.

Cookie event handlers the application assigns on `CookieAuthenticationOptions.Events` keep running, before the
package's. A scheme that sets `EventsType`, or whose events object overrides the events the package relies on, fails
when its options are built.

The tracking row records the client address with the rule the rate limiter uses: `ClientIpHeader` when present and
parseable, otherwise the connection's remote address. A sign-in on a request that still carries a live session cookie
reuses that cookie's key; the row is then attributed to the signing-in account.

Applications report what happened through `ISessionLifecycle`, after their database commit:

| Intent | Typical caller |
| --- | --- |
| `CredentialsChangedAsync` | password change or reset, second factor enabled or disabled |
| `AccessRevokedAsync` (optionally per tenant) | account disabled or deleted, sign-in identity changed, membership removed |
| `ClaimsChangedAsync` (optionally per tenant) | profile, language, roles or permissions changed |
| `TenantRemovedAsync` | tenant deleted |
| `SwitchTenantAsync` | the caller switches tenant |
| `ListMySessionsAsync` / `RevokeMySessionAsync` | the caller's own session management; `RevokeMySessionAsync` returns `false` when the caller's account has no session with that id, for the application to answer 404 |

Hooks the application keeps: `ISessionRevocationParticipant`s for its own credentials, an `ISessionEligibility`, the
cookie callback, its own cookie event handlers, and decorators around `ITicketStore` or `IActiveSessionTracker`
(resolve `DistributedCacheTicketStore` or `ActiveSessionTracker<TContext>` as the inner instance). The expired-session
cleanup runs hourly by default; set `ExpiredSessionCleanup.Interval`, or turn it off with
`ExpiredSessionCleanup.Enabled = false`.

### Service-account credentials

The packages own a service account's credentials from end to end: the secret and its hash, issuing bearer tokens,
the lockout, rotation, revocation, and validating the tokens. The application keeps its endpoints (route, rate limit,
OpenAPI text) and the validation of the account data it accepts, and states intents.

```csharp
services.AddBlueprintServiceAccountAuthentication<ApplicationDbContext>();
```

The settings come from `Blueprint:Security:Jwt` and `Blueprint:Security:ServiceAccountLockout`, which
`Csag.Blueprint.Web` applies to `BlueprintServiceAccountOptions`. The registration needs no signing key, so it can run
unconditionally — also in a host started only to export its OpenAPI document. A configured key must have at least 32
characters besides surrounding white space, checked at startup; a missing key fails the first issuance or validation.

| Intent | Effect |
| --- | --- |
| `IssueTokenAsync(clientId, clientSecret)` | Returns a token (`AccessToken`, `TokenType` = `Bearer`, `ExpiresIn` in seconds) or a refusal: `InvalidCredentials` (401) for an unknown client, a wrong secret or a locked account, `AccountInactive` (403) only for the correct secret of a deactivated account. |
| `CreateCredentialsAsync(serviceAccount)` | Generates the client id and secret, stores the account with the secret's hash, returns the secret once. |
| `RotateSecretAsync(serviceAccountId)` | Generates a new secret; the previous secret and its tokens stop working at once. Looks the account up within the ambient tenant; `null` when there is none. |
| `AccountDeactivatedAsync(serviceAccountId)` / `AccountDeletedAsync(serviceAccountId)` | Ends the account's sessions, after the application committed the change. |

Issuance verifies every secret with the same key derivation, unknown clients included. A wrong secret counts towards
the lockout in one statement, so concurrent attempts all count; a locked account is refused whatever secret it is given.
Every time — issuance, expiry, lockout — comes from the injected `TimeProvider`. Rotation, deactivation and deletion tell
the `ISessionRevocationParticipant`s, with a service-account `SessionSubject`.

Each token refers to a server-side session and carries only `ServiceAccountClaimTypes` claims (`sub`, `name`, `type`,
`sid`). The service-account scheme pins HS256, validates issuer, audience and lifetime with no clock skew, signs and
validates over the key's UTF-8 bytes, and does not map inbound claims. Every request resolves the token's session and
builds the principal from the account's current name, tenant, roles and permissions; register
`IServiceAccountClaimsEnricher`s to add the application's own claims.

Secrets are hashed in ASP.NET Core Identity's `PasswordHasher` format with fixed parameters, so hashes the application
wrote with a default `PasswordHasher` keep verifying. A data seed sets a known secret with
`serviceAccount.SetClientSecret(secret)`.

Three authentication schemes are registered. Each request is authenticated by exactly one scheme:

| Scheme | Role |
| --- | --- |
| `BlueprintAuthenticationSchemes.ServiceAccountToken` | Validates service-account tokens. Endpoints do not list it. |
| `BlueprintAuthenticationSchemes.Bearer` (`"Bearer"`) | Forwards each request to the handler of its token kind; the service-account scheme unless an application token kind added through `BlueprintBearerOptions.AddTokenKind` claims the token. |
| `BlueprintAuthenticationSchemes.SessionOrBearer` | Forwards a request that carries a bearer token to `Bearer`, every other request to the session cookie. |

Expired service-account sessions are deleted by the job `AddBlueprintSessionInfrastructure` registers.

### Tenancy and localization infrastructure

| Component | Purpose |
| --- | --- |
| `TenantService` / `TenantManager` | Reusable tenant access and membership logic. |
| `UtcTenantTimeZoneProvider` | The `ITenantTimeZoneProvider` in effect until the application registers its own: every tenant is in UTC. `AddBlueprintTenancyRuntime` and `AddBlueprintTableViewCore` register it with `TryAdd`. |
| `BlueprintDbStringLocalizer` / `BlueprintDbStringLocalizerFactory` | Database-backed localization infrastructure. |
| `PassThroughStringLocalizer` | Generation-mode localizer that returns keys as-is. |
| `TranslationCacheKeys` | Cache key helpers for localization caching. |
| `MigrationBuilderExtensions` | Translation seeding helpers for migrations. |

### Table view infrastructure

| Component | Purpose |
| --- | --- |
| `TableViewExecutor` | Executes filtering, sorting, counting, pagination, and projection for table-view queries. Filters on instant columns are evaluated in the request's zone, else in the zone of `ITenantTimeZoneProvider`, which it looks up only when the request has filter values. |
| `TableViewCatalogService` | Discovers and filters registered table views by permission. |
| `BlueprintTableViewPreferencesService` | Persists per-user table view preferences. |

## Ownership Boundary

This package owns **reusable infrastructure**, not application composition.

The consuming application still owns:

- the concrete `ApplicationDbContext`
- DI composition and host setup
- app-specific options and policies
- app-specific entities and migrations

