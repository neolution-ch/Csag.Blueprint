---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Add reference-style, revocable service-account sessions

`IServiceAccountSessionManager` (registered by `AddBlueprintSessionInfrastructure`) tracks a server-side `BlueprintServiceAccountSession` per issued service-account JWT and resolves the account's current tenant, roles, and permissions on every request, so revocation, secret rotation, and deactivation take effect immediately. Adds the `IdentityClaimTypes.ServiceAccountSessionId` (`sid`) claim type, the `CacheId.ServiceAccountSession` cache namespace, and an audit exclusion for the new tracking table.

This changes the EF model: `BlueprintDbContext` now maps the `BlueprintServiceAccountSessions` table. Consuming applications need their own EF Core migration for the new table.

`ValidateSessionAsync` takes a `string?`. A token carrying no `sid` claim yields `null` from `FindFirstValue`, and the method is documented to report a missing key as "no session" rather than raise, so the signature no longer forces callers to suppress nullable analysis or add a guard the method already performs.

`RevokeSessionAsync` removes the marker under the key the tracking row stores, not only the key it was called with. The cache compares the key it is handed, while the row predicate becomes SQL string equality, which folds case under a case-insensitive column collation and ignores trailing spaces under every SQL Server collation. A spelling that matched the row but not the marker previously deleted the row and left the marker authorizing the revoked session on the fast path, unreachable by any later revoke because revocation enumerates rows.

A cache that refuses to compose a key no longer surfaces as an exception from the read paths. The byte budget the manager enforces is measured against the cache's default key options, so an application configuring an environment prefix or a schema version has a lower effective ceiling, and the abstraction exposes no way to read either setting back. Such a key can address no stored entry — the same composition refused the write — so `ValidateSessionAsync` reports "no session" and `RevokeSessionAsync` continues to its row delete, instead of turning a malformed `sid` into an error in the authentication pipeline.

Issuance treats everything after the tracking row is committed as an uncancellable obligation. The opportunistic reap of expired rows no longer decides whether a session is issued; a failed marker write removes the marker before the row, because a write that throws may still have been accepted by the backend; and the confirmation that the row survived issuance runs to completion rather than abandoning a live marker on a cancelled request.
