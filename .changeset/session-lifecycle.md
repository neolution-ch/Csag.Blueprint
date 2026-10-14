---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Own the cookie session lifecycle in the packages; applications state intents through `ISessionLifecycle`

Applications wired the session cookie by hand (cookie options, 401/403 handlers, a sign-in event that inserted the tracking row, security-stamp suppression) and drove revocation and refresh through low-level `ISessionManager` calls, each call site choosing its own ordering and cancellation. The packages now own that whole lifecycle; the application registers it with two calls and reports what happened through seven intents.

**New:** `ISessionLifecycle` (Csag.Blueprint.Application), registered by `AddBlueprintSessionInfrastructure`:

| Intent | Effect |
| --- | --- |
| `CredentialsChangedAsync(accountId)` | Ends the account's sessions except the caller's own, when the caller's session belongs to that account; without one, ends all of them. For password change and reset, and a second factor enabled or disabled. |
| `AccessRevokedAsync(accountId)` / `AccessRevokedAsync(accountId, tenantId)` | Ends the account's sessions, everywhere or in one tenant. |
| `ClaimsChangedAsync(accountId)` / `ClaimsChangedAsync(accountId, tenantId)` | Rebuilds the cached claims of the account's sessions, all of them or one tenant's. |
| `TenantRemovedAsync(tenantId)` | Ends every session scoped to the tenant. |
| `SwitchTenantAsync(tenantId)` | Checks membership and moves the caller's session to the tenant; returns `SessionTenantSwitchOutcome` (`Switched`, `NotAMember`, `NoActiveSession`, `SessionExpired`). |
| `ListMySessionsAsync()` | Lists the caller's account's sessions with `ActiveSessionInfo.IsCurrent`; never exposes a session key. |
| `RevokeMySessionAsync(sessionId)` | Ends one of the caller's account's sessions; an id from another account ends nothing. Ending the caller's own session signs the caller out. |

No intent takes a `CancellationToken`: once called it runs to completion, so call it after the change it reports is committed. Failures throw; the revoking intents run every step and then throw one `AggregateException` carrying each failure. Every intent is safe to retry.

**New hooks:**

- `ISessionRevocationParticipant`: every revoking intent calls all registered participants with a `SessionRevocation` (reason, `SessionSubject` of kind user or service account, tenant scope, single session id), so the application's own credentials end alongside the sessions. A failing participant does not stop the others.
- `ISessionEligibility`: answers "may this account hold a session?". Asked at every sign-in (a "no" makes the sign-in throw before any session exists) and at every sliding renewal (a "no" ends the session and clears the cookie). Without a registration every account may hold a session.
- `AddBlueprintCookieSessions(Action<CookieBuilder>)`: turns the Identity application cookie into the session cookie. The callback shapes the cookie (name, `SameSite`, secure policy); the package then applies its fixed behaviour: HTTP-only, the ticket store, the lifetime from `SessionExpirationHours` with sliding expiration, a persistent cookie, 401/403 instead of redirects, and no Identity security-stamp validation. Cookie event handlers the application assigns keep running before the package's; a scheme that sets `EventsType` or overrides those events fails when its options are built.
- `AddBlueprintSessionInfrastructure` takes an optional `Action<BlueprintSessionOptions>`: `ClientIpHeader` (the edge-stamped client address header) and `ExpiredSessionCleanup` (`Enabled`, `Interval`).

**Behaviour change:** The ticket store tracks every session it stores: it writes the ticket, then creates the tracking row with the account, tenant, expiry, user agent and client address of the signing-in request. A sign-in that cannot record its session fails and leaves no ticket. Renewal brings the row in line with the ticket — expiry and tenant. A sign-in on a request that still carries a live session cookie reuses that cookie's session key; the row is then attributed to the signing-in account, with its sign-in time, user agent and address, so revoking that account ends the session and revoking the previous account does not.

**Behaviour change:** The client address recorded for a session follows the rule the package's rate limiting uses (`ClientAddressHttpContextExtensions.TryGetClientAddress`, which `RateLimiterExtensions.TryGetClientPartitionKey` now calls): the configured `ClientIpHeader` when present and parseable, otherwise the connection's remote address.

**Behaviour change:** The session lifetime comes from `Blueprint:Security:SessionExpirationHours` alone: `AddBlueprintDefaultValidatedOptions` sets `BlueprintSessionOptions.Lifetime` from it, the cookie's `ExpireTimeSpan` follows, and a ticket that reaches the store without an expiry gets it too. Sign-in code no longer sets `ExpiresUtc` or `IsPersistent`. Startup fails when the lifetime is not positive.

**Behaviour change:** A hosted job deletes the tracking rows of expired sessions, by default once an hour. Each pass deletes in batches by the existing `ExpiresAt` index, so every replica can run it without coordination. Switch it off with `ExpiredSessionCleanup.Enabled = false`.

**Behaviour change:** Revoking intents keep removing the remaining cached tickets when one removal fails, and a claims refresh that races a revocation removes the tickets it rewrote for sessions whose rows are gone.

**Breaking:** `ISessionManager` is removed with all its methods, and `ActiveSessionInfo.SessionKey` with it; the package's `SessionManager<TUser, TContext>` is internal. Use the intents above.

**Breaking:** `IActiveSessionTracker.ExtendAsync` is replaced by `RenewAsync(SessionTrackingEntry)`, and `TrackAsync(SessionTrackingEntry)` is added. `ActiveSessionTracker<TContext>` takes a `TimeProvider`; `DistributedCacheTicketStore` takes `IOptions<BlueprintSessionOptions>` and a `TimeProvider`, and refuses a ticket without an account identifier. Both are registered with `TryAdd` under their concrete types too, so an application can wrap them.

**Breaking:** `ITenantManager<TUser, TTenant>.IsMemberAsync` moves to the new base interface `ITenantMembershipChecker`, which `AddBlueprintTenantManager` also registers. Callers are unaffected.

No EF model change: no migration is needed.

Consumer migration (`csag-blueprint-web`):

- `Infrastructure/Session/ServiceCollectionExtensions.cs`: replace the body of `AddDistributedSessionAuthentication` with `AddBlueprintSessionInfrastructure<ApplicationUser, ApplicationDbContext>(o => o.ClientIpHeader = <RateLimitingSettings:ClientIpHeader>)` and `AddBlueprintCookieSessions(cookie => { cookie.SecurePolicy = securitySettings.CookieSecurePolicy; cookie.SameSite = SameSiteMode.Lax; })`, keeping the `IRolePermissionResolver` and `PermissionClaimsTransformation` registrations. Delete the `ConfigureApplicationCookie` block (name, `HttpOnly`, `ExpireTimeSpan`, `SlidingExpiration`, the redirect handlers, `OnSignedIn`), `OnSignedInAsync`, `ExtractTenantIdFromClaims`, the security-stamp `PostConfigure`, and the `sessionLifetime` parameter.
- `LoginCompletionService.SignInUserAsync`: stop setting `ExpiresUtc` (and `IsPersistent`/`AllowRefresh`).
- Replace the session calls, each after its commit and without a `CancellationToken`:
  - `ChangePasswordEndpoint`, MFA `EnableAuthenticatorEndpoint` and `DisableEndpoint`: the `GetCurrentSessionKeyAsync` + `RevokeOtherUserSessionsAsync`/`RevokeUserSessionsAsync` block becomes `CredentialsChangedAsync(user.Id)`.
  - `ResetPasswordEndpoint`: `CredentialsChangedAsync(user.Id)`.
  - `DisableAccountEndpoint`, `UpdateAccountEndpoint` (login change): `AccessRevokedAsync(id)`.
  - `DeleteUserEndpoint`, `UpdateAccountEndpoint` (membership removals), `RevokeAllSessionsEndpoint`: `AccessRevokedAsync(id, tenantId)`. The intent returns no count, so `RevokeAllSessionsResponse.SessionsRevoked` goes or is computed by the endpoint.
  - `UpdateUserEndpoint`, `SwitchLanguageEndpoint`, `UpdateAccountEndpoint` (other changes): `ClaimsChangedAsync(id)` or `ClaimsChangedAsync(id, tenantId)`.
  - `DeleteTenantEndpoint`: `TenantRemovedAsync(id)`.
  - `SwitchTenantEndpoint`: the membership check, session lookup, ticket-store retrieve/renew and rollback become `SwitchTenantAsync(req.TenantId)`; map `NotAMember` to its 400 and `NoActiveSession`/`SessionExpired` to its 401.
  - `ListMySessionsEndpoint`: `ListMySessionsAsync()`, using `IsCurrent`.
  - `RevokeMySessionEndpoint`: `RevokeMySessionAsync(req.SessionId)`. It does not report whether the id was found; keep the endpoint's own ownership lookup if the 404 must stay.
- Optionally register an `ISessionEligibility` (for example: a disabled or locked-out account may not hold a session) and `ISessionRevocationParticipant`s for the application's own credentials.
- Tests: the package now covers what `SessionManagerTests`, `SessionLifetimeConfigurationTests` and `SwitchTenantSessionTargetingTests` checked; keep one HTTP test per behaviour the application relies on.
