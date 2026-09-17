# @neolution-ch/csag-blueprint-infrastructure

## 0.3.0

### Minor Changes

- [#41](https://github.com/neolution-ch/Csag.Blueprint/pull/41) [`42128c7`](https://github.com/neolution-ch/Csag.Blueprint/commit/42128c7afd2f093d58c224c6311b6413dd7b3f11) Thanks [@neotrow](https://github.com/neotrow)! - Move session untracking into the ticket store so consumers never need an `OnSigningOut` handler
  
  `DistributedCacheTicketStore.RemoveAsync` now deletes the `BlueprintActiveSessions` row alongside the cache entry. Untracking previously had to be wired by each consuming application as a cookie `OnSigningOut` handler — a responsibility that cannot be discharged safely, because `CookieSigningOutContext` does not carry the session key and the only way to recover it is `HttpContext.AuthenticateAsync`. When sign-out is raised from inside the cookie handler's own authentication pass (as `SecurityStampValidator` does), that call returns the still-running authenticate task and the request awaits itself forever.
  
  Expiry is not the path this is built around. `TicketCacheService` caches every ticket with `AbsoluteExpiration` set to the ticket's own `ExpiresUtc`, so the cache entry dies with the ticket and `CookieAuthenticationHandler` normally fails the request on the null `RetrieveAsync` result rather than reaching the branch that discards an expired ticket. Where clock skew or a retrieve straddling the expiry instant does reach that branch, it calls `RemoveAsync`, which now untracks like any other removal. Rows of expired sessions removed no other way are reaped by `ISessionManager.CleanupExpiredSessionsAsync`, which consuming applications schedule; `GetUserSessionsAsync` already filters them out of session listings by `ExpiresAt`.
  
  **Breaking:** `ISessionExpirationExtender` is now `IActiveSessionTracker` and gains `UntrackAsync`; `SessionExpirationExtender<TContext>` is now `ActiveSessionTracker<TContext>`, and `DistributedCacheTicketStore`'s constructor takes the renamed abstraction. Applications that only consume `AddBlueprintSessionInfrastructure` need no change beyond deleting their own `OnSigningOut` handler; one that constructs the ticket store by hand updates that argument's type.
  
  **Breaking:** `ISessionManager.UntrackSessionAsync` is removed. It deleted the tracking row while leaving the cached ticket live, producing a session that `GetUserSessionsAsync` cannot list and that no revocation path can reach — `RevokeUserSessionsAsync`, `RevokeOtherUserSessionsAsync` and `RevokeTenantSessionsAsync` all snapshot the rows first. The ticket store now untracks on its own; a caller that wants to end a session by key uses `RevokeSessionAsync`, which removes both the ticket and the row.
  
  **Behaviour change:** Both by-key removal paths — `DistributedCacheTicketStore.RemoveAsync` and `ISessionManager.RevokeSessionAsync` — delete the tracking row before removing the cached ticket, matching `RevokeSessionsCoreAsync`. A sliding renewal re-writes its ticket unconditionally and only then extends its row, so with the row already gone that extension reports "no row" and the renewal removes its own ticket. The opposite order left a window in which a renewal racing a revocation resurrected the ticket and extended the still-present row, and the untracking then stripped the tracking row off a live session: invisible to `GetUserSessionsAsync`, unreachable by every revocation path, since they all snapshot rows first. `RevokeSessionAsync` removes the ticket even when no row matched, so a session already in that state can still be ended by key.
  
  **Behaviour change:** `DistributedCacheTicketStore` no longer writes session keys to its logs. The key is a bearer credential carried by the authentication cookie, so anyone with log access could replay a session from it. Its three warning messages now carry a `SessionTag` instead: the first six bytes of the key's SHA-256, hex-encoded, which correlates occurrences of one session without being reversible. Log queries, dashboards or alerts matching on the logged session key need updating.
  
  **Behaviour change:** `PostConfigureCookieAuthenticationOptions` now installs the ticket store only on `IdentityConstants.ApplicationScheme`. It previously ignored the options name and applied to every cookie scheme, including the external and two-factor cookies, which have no tracking rows — without this scoping, the new untracking would run a `DELETE` on every one of their removals.

- [#37](https://github.com/neolution-ch/Csag.Blueprint/pull/37) [`05103b1`](https://github.com/neolution-ch/Csag.Blueprint/commit/05103b1f33f6c17ef5e8745d11fef3c2acdf23a9) Thanks [@neotrow](https://github.com/neotrow)! - Add reference-style, revocable service-account sessions
  
  `IServiceAccountSessionManager` (registered by `AddBlueprintSessionInfrastructure`) tracks a server-side `BlueprintServiceAccountSession` per issued service-account JWT and resolves the account's current tenant, roles, and permissions on every request, so revocation, secret rotation, and deactivation take effect immediately. Adds the `IdentityClaimTypes.ServiceAccountSessionId` (`sid`) claim type, the `CacheId.ServiceAccountSession` cache namespace, and an audit exclusion for the new tracking table.
  
  This changes the EF model: `BlueprintDbContext` now maps the `BlueprintServiceAccountSessions` table. Consuming applications need their own EF Core migration for the new table.
  
  `ValidateSessionAsync` takes a `string?`. A token carrying no `sid` claim yields `null` from `FindFirstValue`, and the method is documented to report a missing key as "no session" rather than raise, so the signature no longer forces callers to suppress nullable analysis or add a guard the method already performs.
  
  `RevokeSessionAsync` removes the marker under the key the tracking row stores, not only the key it was called with. The cache compares the key it is handed, while the row predicate becomes SQL string equality, which folds case under a case-insensitive column collation and ignores trailing spaces under every SQL Server collation. A spelling that matched the row but not the marker previously deleted the row and left the marker authorizing the revoked session on the fast path, unreachable by any later revoke because revocation enumerates rows.
  
  A cache that refuses to compose a key no longer surfaces as an exception from the read paths. The byte budget the manager enforces is measured against the cache's default key options, so an application configuring an environment prefix or a schema version has a lower effective ceiling, and the abstraction exposes no way to read either setting back. Such a key can address no stored entry — the same composition refused the write — so `ValidateSessionAsync` reports "no session" and `RevokeSessionAsync` continues to its row delete, instead of turning a malformed `sid` into an error in the authentication pipeline.
  
  Issuance treats everything after the tracking row is committed as an uncancellable obligation. The opportunistic reap of expired rows no longer decides whether a session is issued; a failed marker write removes the marker before the row, because a write that throws may still have been accepted by the backend; and the confirmation that the row survived issuance runs to completion rather than abandoning a live marker on a cancelled request.

- [#39](https://github.com/neolution-ch/Csag.Blueprint/pull/39) [`2accfef`](https://github.com/neolution-ch/Csag.Blueprint/commit/2accfef631d67e42fd048e6dc3429b7216c6b8aa) Thanks [@neotrow](https://github.com/neotrow)! - Read all blueprint timestamps through an injected `TimeProvider`
  
  Every blueprint type that stamped or compared a time read `DateTimeOffset.UtcNow` inline, so any
  consumer behaviour that depends on the clock — audit stamps, session expiry, session cleanup, tenant
  membership join times, table view preference timestamps — could only be tested by sleeping. The
  clock is now a constructor dependency:
  
  - `AuditableTimestampInterceptor` takes a `TimeProvider`.
  - `SessionManager<TUser, TContext>` takes a `TimeProvider`. `GetUserSessionsAsync` and
    `CleanupExpiredSessionsAsync` read it once per call and compare against that instant rather than
    re-reading the clock inside the EF predicate.
  - `TenantManager<TUser, TTenant, TContext>` takes a `TimeProvider`.
  - `BlueprintTableViewPreferencesService<TContext, TUser>` takes a `TimeProvider`.
  - The `CreatedAt` column on `BlueprintAuditLogs` is stamped from the `TimeProvider` resolved out of
    the application's services, falling back to `TimeProvider.System` when `ConfigureBlueprintAuditLogging`
    is called without any of the registrations below, so audit logging stays wirable on its own.
  
  `AddBlueprintServices`, `AddBlueprintTenancyRuntime`, `AddBlueprintSessionInfrastructure` and
  `AddBlueprintTableViewPreferences` each `TryAddSingleton(TimeProvider.System)`, so the default needs
  no wiring and a `FakeTimeProvider` registered before any of them wins. The `TryAdd` matters for
  consumers that use `Csag.Blueprint.Infrastructure` without the Web package — a seeding console app,
  for example — which never call `AddBlueprintServices`.
  
  **Breaking for consumers who construct these types themselves.** Every type listed above gained a
  required `TimeProvider` constructor parameter, so `SessionManager<TUser, TContext>`,
  `TenantManager<TUser, TTenant, TContext>` and `BlueprintTableViewPreferencesService<TContext, TUser>`
  no longer compile against their previous constructors either. Applications that resolve them from the
  container need no change, because the registrations above supply the clock.
  
  `AuditableTimestampInterceptor` is the one that usually needs hand-wiring: it no longer has a
  parameterless constructor, and `AddBlueprintTenancyRuntime` now registers it as a singleton alongside
  `TenantSaveInterceptor`. Resolve it from the container when wiring the pooled context factory:
  
  ```csharp
  services.AddPooledDbContextFactory<ApplicationDbContext>((sp, options) =>
  {
      var tenantInterceptor = sp.GetRequiredService<TenantSaveInterceptor>();
      var timestampInterceptor = sp.GetRequiredService<AuditableTimestampInterceptor>();
      options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
          .AddInterceptors(new AuditSaveChangesInterceptor(), timestampInterceptor, tenantInterceptor);
  });
  ```
  
  The singleton lifetime is unchanged and still safe across pooled `DbContext` instances: the
  interceptor holds only the ambient `CurrentActorContext` and a singleton `TimeProvider`, neither of
  which is scoped state.
  
  No schema change, so consumers need no migration.

### Patch Changes

- [#37](https://github.com/neolution-ch/Csag.Blueprint/pull/37) [`05103b1`](https://github.com/neolution-ch/Csag.Blueprint/commit/05103b1f33f6c17ef5e8745d11fef3c2acdf23a9) Thanks [@neotrow](https://github.com/neotrow)! - Validate the session key and clamp the diagnostic fields in `ISessionManager`
  
  `TrackSessionAsync` rejects a blank session key, or one whose URL-encoded form exceeds the 231 bytes the
  distributed cache leaves for a key under `CacheId.AuthTicket`, and does so synchronously at the call site
  before any I/O. It also clamps `userAgent` and `ipAddress` to their mapped column lengths, so an over-length
  client `User-Agent` header can no longer fail the insert and leave a cached ticket with no tracking row.
  
  `RevokeSessionAsync` reports such a key as "no session" rather than passing it to the
  ticket cache, which throws on an over-long key and silently redirects a blank one to the shared
  `CacheId.AuthTicket` entry. `RevokeOtherUserSessionsAsync` now rejects a `keepSessionKey` that no tracked session could carry — a
  whitespace-only one, which previously passed its non-empty check, and one over the cache-key budget, which
  `TrackSessionAsync` refuses to store. Either degraded the filter to "revoke every session", signing out the
  very session the caller asked to keep.

- [#37](https://github.com/neolution-ch/Csag.Blueprint/pull/37) [`05103b1`](https://github.com/neolution-ch/Csag.Blueprint/commit/05103b1f33f6c17ef5e8745d11fef3c2acdf23a9) Thanks [@neotrow](https://github.com/neotrow)! - Let `BlueprintActiveSessions` use its sequential primary-key default
  
  `BlueprintActiveSessionConfiguration` has always given `Id` a `NEWSEQUENTIALID()` store default, but
  `SessionManager.TrackSessionAsync` assigned a random `Guid` before every insert, so the default never
  applied and the clustered key fragmented on this insert-heavy table. The key is now left to the database.
  No migration is required — the column default is unchanged, and existing rows keep their keys.

## 0.2.0

### Minor Changes

- [#27](https://github.com/neolution-ch/Csag.Blueprint/pull/27) [`92310d6`](https://github.com/neolution-ch/Csag.Blueprint/commit/92310d656e62d5006c5792b0899224d71e5986fa) Thanks [@neotrow](https://github.com/neotrow)! - Infrastructure behavior fixes:
  
  - The global tenant query filter now compares `TenantId` against the ambient tenant in lifted nullable form (`e.TenantId == context.CurrentTenantId`). With no ambient tenant, tenant-owned queries deterministically return no rows (fail-closed by empty result) instead of throwing `InvalidOperationException("Nullable object must have a value")`. Callers that need cross-tenant access must use `IgnoreQueryFilters()` deliberately — including idempotent seeders, which must set the ambient tenant before existence checks.
  - `SessionClaimsHelper.ApplySessionClaims` removes any existing `TenantId` claim when rebuilding a session without a tenant, so a tenant-less session carries no tenant claim (matching its documented contract).
  - `UserClaimsHelper` profile claim replacement removes all existing claims of a type before adding the fresh value, consistent with the role/permission/tenant helpers; stale duplicates can no longer survive a ticket refresh.
  - `TranslationProvider` and `TranslationCacheInvalidator` normalize language codes to canonical lowercase for cache keys, database lookups, and the requested-vs-default-language comparison, so translation resolution no longer depends on caller casing or database collation. Translation rows are expected to store the canonical lowercase code.

### Patch Changes

- [#34](https://github.com/neolution-ch/Csag.Blueprint/pull/34) [`338208d`](https://github.com/neolution-ch/Csag.Blueprint/commit/338208d4cb833eadfb2aae7fd0bb48447ad17241) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 3 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | FastEndpoints | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Swagger | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Testing | 8.2.0 | 8.3.0 | 🟡 minor |

## 0.1.3

### Patch Changes

- [#23](https://github.com/neolution-ch/Csag.Blueprint/pull/23) [`e44bd77`](https://github.com/neolution-ch/Csag.Blueprint/commit/e44bd77c56acd2e391c77ee09540ef292ae7260b) Thanks [@LarsMarty](https://github.com/LarsMarty)! - Add a TenantId column to audit log entries
  
  Consuming applications need their own EF Core migration to add the nullable TenantId column.

- [#22](https://github.com/neolution-ch/Csag.Blueprint/pull/22) [`4cdb315`](https://github.com/neolution-ch/Csag.Blueprint/commit/4cdb315d90d7085db77400b7defac4ea6c08adf4) Thanks [@neotrow](https://github.com/neotrow)! - Update all dependencies to their latest versions and regenerate the lock files
  so transitive dependencies are refreshed as well.
  
  **Consuming these packages now requires the .NET SDK 10.0.4xx feature band
  (10.0.400 or newer).** `Csag.Blueprint.SourceGenerators` is built against Roslyn
  5.9.0, which ships only in that band, and the compiler refuses to load an
  analyzer referencing a Roslyn newer than the one running the build. On an older
  SDK the translation-key generator is skipped with a `CS9057` warning and the
  generated types go missing, which surfaces as `CS0103`/`CS0246` errors rather
  than as an obvious SDK problem. IDEs run their own Roslyn for design-time
  generation, so Visual Studio and Rider need to be new enough too. See the SDK
  requirement table in the README.
  
  `Microsoft.ApplicationInsights` stays on the 2.x branch.
  
  `Csag.Blueprint.Web` no longer pins `Microsoft.Data.SqlClient` below the
  centrally managed version and now resolves 7.0.2 in line with the other
  packages, which also brings its `Microsoft.Data.SqlClient.Extensions.Abstractions`
  and `.Internal.Logging` dependencies up from 1.0.0 to 7.0.2.

## 0.1.2

### Patch Changes

- [#20](https://github.com/neolution-ch/Csag.Blueprint/pull/20) [`453255b`](https://github.com/neolution-ch/Csag.Blueprint/commit/453255bf2f5aa0c1cf0aebf0750a19b9dce4f5ba) Thanks [@neoscie](https://github.com/neoscie)! - Add the email address and the display name of the user to audit events

  Audit events now contain `UserEmail` and `UserDisplayName` with `UserId`. The package reads these
  values from the claims on the request when it writes the event. One helper reads the claims for the
  Entity Framework path and for the HTTP path. Therefore an application can show the name of the user
  without a query on the user table.

  A service account has no email address. Therefore its email value is null, and its display name is
  the account name from the token.

  The package writes the two new values to the `JsonData` column, at `$.UserEmail` and
  `$.UserDisplayName`. This release does not change the schema. An application does not need a
  migration.

## 0.1.1

### Patch Changes

- [#15](https://github.com/neolution-ch/Csag.Blueprint/pull/15) [`53adf9c`](https://github.com/neolution-ch/Csag.Blueprint/commit/53adf9c6f519a6306c25bda3b68b6848a7db9496) Thanks [@neotrow](https://github.com/neotrow)! - Update to the latest blueprint packages from csag-blueprint-web

## 0.1.0

### Minor Changes

- [`428bb6f`](https://github.com/neolution-ch/Csag.Blueprint/commit/428bb6fc58a912f5f0d53e0a64799e12c90a8ad4) Thanks [@neotrow](https://github.com/neotrow)! - initial release
