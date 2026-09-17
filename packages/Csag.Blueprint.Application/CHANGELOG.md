# @neolution-ch/csag-blueprint-application

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

## 0.2.0

### Minor Changes

- [#27](https://github.com/neolution-ch/Csag.Blueprint/pull/27) [`92310d6`](https://github.com/neolution-ch/Csag.Blueprint/commit/92310d656e62d5006c5792b0899224d71e5986fa) Thanks [@neotrow](https://github.com/neotrow)! - Web, Application, and Testing fixes:
  
  - `SecuritySettingsValidator` and `LocalizationOptionsValidator` report proper validation errors instead of throwing `NullReferenceException` when `CorsPolicies` / `SupportedLanguages` are null; `LocalizationOptions.TranslationCacheL1ExpirationMinutes` is now validated (must be greater than 0).
  - `AddConfiguredCors` fails fast at wiring time with a clear `InvalidOperationException` for a wildcard origin mixed with explicit origins, and for the wildcard-plus-`AllowCredentials` combination — both previously surfaced late or not at all.
  - `TenantMiddleware` clears any pre-existing ambient tenant before invoking downstream when the resolver yields no tenant, so stale ambient state never flows into request handling.
  - `JwtSettingsValidator` no longer requires `SigningKey` to be present (generation-mode startup has no key; presence is enforced by the host at runtime as documented) — a key that *is* provided must still be at least 32 characters, measured after trimming, so a whitespace-only or whitespace-padded value cannot pass as key material.
  - TableView filters: numeric ranges support negative bounds (`"-5-10"` parses as -5..10), undefined enum values are rejected instead of silently matching nothing, `Equals` works on boolean columns, and `Filterable()` no longer wipes the auto-derived enum `allowedValues` from column metadata when called without an explicit list.
  - `MigrationBuilderExtensions.SeedTranslation(s)` normalizes language codes to canonical lowercase so seeded rows always match translation lookups.
  - `MsSqlTestContainerOrchestrator` no longer contacts the Docker daemon at construction time (the container is built in `StartAsync`), and the missing-`Initial Catalog` error no longer embeds the connection string (which contains the SA password). `ShouldHaveStatusCodeAsync` accepts an optional `CancellationToken` — binary-breaking for assemblies compiled against the previous version; recompile against this one.

### Patch Changes

- [#34](https://github.com/neolution-ch/Csag.Blueprint/pull/34) [`338208d`](https://github.com/neolution-ch/Csag.Blueprint/commit/338208d4cb833eadfb2aae7fd0bb48447ad17241) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 3 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | FastEndpoints | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Swagger | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Testing | 8.2.0 | 8.3.0 | 🟡 minor |

## 0.1.3

### Patch Changes

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
