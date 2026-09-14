---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Move session untracking into the ticket store so consumers never need an `OnSigningOut` handler

`DistributedCacheTicketStore.RemoveAsync` now deletes the `BlueprintActiveSessions` row alongside the cache entry. Untracking previously had to be wired by each consuming application as a cookie `OnSigningOut` handler — a responsibility that cannot be discharged safely, because `CookieSigningOutContext` does not carry the session key and the only way to recover it is `HttpContext.AuthenticateAsync`. When sign-out is raised from inside the cookie handler's own authentication pass (as `SecurityStampValidator` does), that call returns the still-running authenticate task and the request awaits itself forever.

Expiry is deliberately not part of this. `TicketCacheService` caches every ticket with `AbsoluteExpiration` set to the ticket's own `ExpiresUtc`, so the cache entry dies with the ticket and `CookieAuthenticationHandler` fails the request on the null `RetrieveAsync` result before it can discard an expired ticket. The tracking rows of expired sessions are reaped by `ISessionManager.CleanupExpiredSessionsAsync`, which consuming applications schedule; `GetUserSessionsAsync` already filters them out of session listings by `ExpiresAt`.

**Breaking:** `ISessionExpirationExtender` is now `IActiveSessionTracker` and gains `UntrackAsync`; `SessionExpirationExtender<TContext>` is now `ActiveSessionTracker<TContext>`. Applications that only consume `AddBlueprintSessionInfrastructure` need no change beyond deleting their own `OnSigningOut` handler.

**Breaking:** `ISessionManager.UntrackSessionAsync` is removed. It deleted the tracking row while leaving the cached ticket live, producing a session that `GetUserSessionsAsync` cannot list and that no revocation path can reach — `RevokeUserSessionsAsync`, `RevokeOtherUserSessionsAsync` and `RevokeTenantSessionsAsync` all snapshot the rows first. The ticket store now untracks on its own; a caller that wants to end a session by key uses `RevokeSessionAsync`, which removes both the ticket and the row.

**Behaviour change:** `PostConfigureCookieAuthenticationOptions` now installs the ticket store only on `IdentityConstants.ApplicationScheme`. It previously ignored the options name and applied to every cookie scheme, including the external and two-factor cookies, which have no tracking rows — without this scoping, the new untracking would run a `DELETE` on every one of their removals.
