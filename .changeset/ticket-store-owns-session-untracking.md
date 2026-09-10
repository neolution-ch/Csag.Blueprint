---
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Move session untracking into the ticket store so consumers never need an `OnSigningOut` handler

`DistributedCacheTicketStore.RemoveAsync` now deletes the `BlueprintActiveSessions` row alongside the cache entry. Untracking previously had to be wired by each consuming application as a cookie `OnSigningOut` handler — a responsibility that cannot be discharged safely, because `CookieSigningOutContext` does not carry the session key and the only way to recover it is `HttpContext.AuthenticateAsync`. When sign-out is raised from inside the cookie handler's own authentication pass (as `SecurityStampValidator` does), that call returns the still-running authenticate task and the request awaits itself forever.

This also fixes a second bug: the cookie handler calls `RemoveAsync` directly when it discards an expired ticket, a path that never raised `OnSigningOut`. Sessions that expired rather than being explicitly logged out left their tracking row behind permanently.

**Breaking:** `ISessionExpirationExtender` is now `IActiveSessionTracker` and gains `UntrackAsync`; `SessionExpirationExtender<TContext>` is now `ActiveSessionTracker<TContext>`. Applications that only consume `AddBlueprintSessionInfrastructure` need no change beyond deleting their own `OnSigningOut` handler.

**Behaviour change:** `PostConfigureCookieAuthenticationOptions` now installs the ticket store only on `IdentityConstants.ApplicationScheme`. It previously ignored the options name and applied to every cookie scheme, including the external and two-factor cookies, which have no tracking rows — without this scoping, the new untracking would run a `DELETE` on every one of their removals.
