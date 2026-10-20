# @neolution-ch/csag-blueprint-infrastructure

## 0.5.0

### Minor Changes

- [#57](https://github.com/neolution-ch/Csag.Blueprint/pull/57) [`7a35cac`](https://github.com/neolution-ch/Csag.Blueprint/commit/7a35cac38b84e89df719cd26c29ac7134b9a165b) Thanks [@neotrow](https://github.com/neotrow)! - Own service-account credentials in the packages; applications state intents through `IServiceAccountCredentials` and `IServiceAccountSessionLifecycle`
  
  Applications issued service-account tokens themselves (secret verification, lockout, session tracking, minting), generated and hashed secrets, revoked sessions by hand on rotation, deactivation and deletion, and registered and validated the bearer scheme. The packages now own that whole credential lifecycle; the application registers it with one call and keeps thin endpoint shells.
  
  **New:** `IServiceAccountCredentials` (Csag.Blueprint.Application), registered by `AddBlueprintServiceAccountAuthentication<TContext>`:
  
  | Intent | Effect |
  | --- | --- |
  | `IssueTokenAsync(clientId, clientSecret)` | Returns a `ServiceAccountTokenResult`: `Issued` with `AccessToken`, `TokenType` (`Bearer`) and `ExpiresIn` (seconds), or `InvalidCredentials` (unknown client, wrong secret, locked account) or `AccountInactive` (correct secret, deactivated account). |
  | `CreateCredentialsAsync(serviceAccount)` | Generates the client id and secret, stores the account with the secret's hash, returns `ServiceAccountClientCredentials` (client id and secret) once. |
  | `RotateSecretAsync(serviceAccountId)` | Generates a new secret and returns it once; the previous secret and every token issued with it stop working at once. Looks the account up within the ambient tenant and returns `null` when there is none. |
  
  **New:** `IServiceAccountSessionLifecycle`, next to `ISessionLifecycle`: `AccountDeactivatedAsync(serviceAccountId)` and `AccountDeletedAsync(serviceAccountId)` end the account's sessions. Rotation, deactivation and deletion call every `ISessionRevocationParticipant` with a `SessionSubject` of kind `ServiceAccount`.
  
  No intent takes a `CancellationToken`; call the session intents after the change they report is committed. Failures throw, after every step has run.
  
  **New:** `IServiceAccountClaimsEnricher` adds the application's claims to a service-account principal on every request. Tokens stay references to their session and carry no claims beyond `ServiceAccountClaimTypes`.
  
  **New:** `ServiceAccountClaimTypes` (Csag.Blueprint.Application) names the claims of a service-account token and principal: `Subject` (`sub`), `Name` (`name`), `Type` (`type`, value `ServiceAccountType`) and `SessionId` (`sid`).
  
  **New:** `AddBlueprintServiceAccountAuthentication<TContext>(Action<BlueprintServiceAccountOptions>?)` (Csag.Blueprint.Infrastructure) registers the intents, the service-account sessions and three authentication schemes: `BlueprintAuthenticationSchemes.ServiceAccountToken`, which validates service-account tokens; `Bearer`, which forwards each request to the handler of its token kind (`BlueprintBearerOptions.AddTokenKind` adds the application's own kinds); and `SessionOrBearer`, which forwards a request carrying a bearer token to `Bearer` and every other request to the session cookie. The registration needs no signing key, so it runs in OpenAPI generation mode too; a configured key is checked at startup and a missing key fails the first issuance or validation. `Csag.Blueprint.Web`'s `AddBlueprintDefaultValidatedOptions` fills `BlueprintServiceAccountOptions` from `Blueprint:Security:Jwt` and `Blueprint:Security:ServiceAccountLockout`; the settings themselves are unchanged.
  
  **New:** `SetClientSecret(this BlueprintServiceAccount, string)` sets a known secret, for data seeds and test fixtures.
  
  **Behaviour:** Issuance verifies every secret with the same key derivation, unknown clients included. A wrong secret counts towards the lockout in one statement, so concurrent attempts all count; while locked, an account is refused whatever secret it is given, and further wrong secrets do not count. A correct secret clears the count in one statement that matches only while the account is not locked and still stores that secret; a secret a rotation replaced in the meantime is refused as `InvalidCredentials`, whatever the account's state, and leaves the count as it is. `AccountInactive` is answered only to the correct secret; a wrong secret for a deactivated account is `InvalidCredentials` and counts. Every time — issuance, expiry, lockout — comes from the injected `TimeProvider`.
  
  **Behaviour:** The service-account scheme pins HS256, validates issuer, audience and lifetime with no clock skew against the injected `TimeProvider`, signs and validates over the signing key's UTF-8 bytes, does not map inbound claims, and answers a refused token with a bare `WWW-Authenticate: Bearer`. Every request resolves the token's session and builds the principal from the account's current client id, name, tenant, roles and permissions; a token whose subject does not own its session is refused. Tokens issued before the upgrade keep working when the configured key is ASCII.
  
  **Behaviour:** Secrets are hashed in ASP.NET Core Identity's `PasswordHasher` format with fixed parameters; hashes written by a default `PasswordHasher` keep verifying. Generated secrets are 256 random bits, base64url-encoded.
  
  **Behaviour change:** Each request is authenticated by exactly one scheme. With `CookieAuthMode` and `JwtAuthMode` both `OptOut`, `UseFastEndpointsWithConventions` gives authenticated endpoints the one `SessionOrBearer` scheme instead of the cookie and `Bearer` schemes side by side. An endpoint that declares its own `AuthSchemes()` keeps exactly those, without the default added.
  
  **Behaviour change:** `UseBlueprintMiddleware` refuses a request that carries both the session cookie and a bearer token with a 400 ProblemDetails carrying the correlation id, before authentication runs (`SingleCredentialMiddleware`).
  
  **Behaviour change:** `ExpiredSessionCleanupService` deletes expired `BlueprintServiceAccountSessions` rows in the same pass as the user session rows.
  
  **Behaviour change:** `CurrentActorMiddleware` stamps a service account with its raw `sub` claim only.
  
  **Breaking:** `IServiceAccountSessionManager` and `ServiceAccountSessionValidation` are removed from Csag.Blueprint.Application; the package's session manager is internal and registered by `AddBlueprintServiceAccountAuthentication`, not by `AddBlueprintSessionInfrastructure`. Use the intents above.
  
  **Breaking:** `IdentityClaimTypes.ServiceAccountSessionId` is removed; use `ServiceAccountClaimTypes.SessionId`.
  
  **Breaking:** The package registers the `Bearer` scheme; an application that registers its own `Bearer` scheme fails at startup.
  
  Csag.Blueprint.Infrastructure references `Microsoft.AspNetCore.Authentication.JwtBearer`. No EF model change: no migration is needed.
  
  Consumer migration (`csag-blueprint-web`):
  
  - `Extensions/ServiceCollectionExtensions/ApplicationServicesExtensions.cs`: replace `.AddServiceAccountPasswordHasher()` with `.AddBlueprintServiceAccountAuthentication<ApplicationDbContext>()`, unconditionally (it serves generation mode too), and delete `builder.Services.AddJwtAuthentication(jwtSettings)` from `AddRuntimeServices`. The runtime signing-key presence check there may stay, so a host without a key fails at startup rather than on its first token.
  - Delete `Extensions/ServiceCollectionExtensions/JwtExtensions.cs`: the hasher registration, the bearer registration and its validation parameters, and the token-validated handler with its `ClaimTypes.Sid` fallback.
  - Delete any app-side check that refuses requests carrying both a session cookie and a bearer token; `UseBlueprintMiddleware` does it.
  - Keep `CookieAuthMode` and `JwtAuthMode` `OptOut` in `Program.cs`. The MFA endpoints that declare `AuthSchemes(IdentityConstants.ApplicationScheme, IdentityConstants.TwoFactorUserIdScheme)` keep exactly those two.
  - Map the endpoints onto the intents, each keeping its route, rate limit, OpenAPI text and validation:
    - `Auth/Token/TokenEndpoint`: the body becomes `IssueTokenAsync(req.ClientId, req.ClientSecret)`; `Issued` maps to 200 with `{ accessToken, tokenType, expiresIn }` from `AccessToken`, `TokenType` and `ExpiresIn`, `AccountInactive` to 403, `InvalidCredentials` to 401. Delete the dummy hash, the lockout helpers, the session tracking, the hash re-read and the `JwtBearer.CreateToken` call.
    - `ServiceAccounts/Create`: keep the validator and the tenant check; map the request to a `BlueprintServiceAccount` without client id or hash, then `CreateCredentialsAsync(serviceAccount)` and set `response.ClientSecret` from its result. Delete `GenerateClientId`, `GenerateClientSecret` and the client-id uniqueness check.
    - `ServiceAccounts/RotateSecret`: `RotateSecretAsync(req.Id)`; `null` maps to 404. Delete the hashing, both revocation passes and the secret generator.
    - `ServiceAccounts/Update`: after the commit, when the account was deactivated, `AccountDeactivatedAsync(serviceAccount.Id)`.
    - `ServiceAccounts/Delete`: after the commit, `AccountDeletedAsync(serviceAccount.Id)`.
  - Replace `IdentityClaimTypes.ServiceAccountSessionId` and the `"sub"`, `"name"`, `"type"`, `"ServiceAccount"` literals with `ServiceAccountClaimTypes`.
  - `Infrastructure.DataSeeding/Seeds/ServiceAccountSeed.cs`: set the test secret with `account.SetClientSecret(TestClientSecret)` and drop the `IPasswordHasher<BlueprintServiceAccount>` dependency; delete the `IPasswordHasher<BlueprintServiceAccount>` registrations in `Cli.DataSeeding/Startup.cs` and the integration test fixture.
  - Drop the `FastEndpoints.Security` package reference when nothing else uses it.
  - Optionally register `IServiceAccountClaimsEnricher`s for per-request claims, and `ISessionRevocationParticipant`s that handle `SessionSubjectKind.ServiceAccount`.
  - Tests: the package now covers issuance, lockout (including concurrent attempts), rotation, revocation and bearer validation; keep one HTTP test per behaviour the application relies on, such as the token endpoint's status mapping and its rate limit.

- [#56](https://github.com/neolution-ch/Csag.Blueprint/pull/56) [`11189d5`](https://github.com/neolution-ch/Csag.Blueprint/commit/11189d5d69415a20dc9f2cc787656f61ae3d903e) Thanks [@neotrow](https://github.com/neotrow)! - Own the cookie session lifecycle in the packages; applications state intents through `ISessionLifecycle`
  
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
  | `RevokeMySessionAsync(sessionId)` | Ends one of the caller's account's sessions and returns `true`; returns `false` without ending anything or calling a participant when the caller's account has no session with that id (unknown, already ended, or another account's). Ending the caller's own session signs the caller out; when it could not be ended, the caller stays signed in and the intent can be retried. |
  
  No intent takes a `CancellationToken`: once called it runs to completion, so call it after the change it reports is committed. Failures throw; the revoking intents run every step and then throw one `AggregateException` carrying each failure. Every intent is safe to retry: a session whose cached ticket or tracking row could not be removed stays tracked, so the retry ends it and calls the participants again. A retried `RevokeMySessionAsync` whose session already ended (only a participant failed) returns `false` and calls no participant again.
  
  **New hooks:**
  
  - `ISessionRevocationParticipant`: every revoking intent calls all registered participants with a `SessionRevocation` (reason, `SessionSubject` of kind user or service account, tenant scope, single session id), so the application's own credentials end alongside the sessions. A failing participant does not stop the others.
  - `ISessionEligibility`: answers "may this account hold a session?". Asked at every sign-in (a "no" makes the sign-in throw before any session exists) and at every sliding renewal (a "no" ends the session and clears the cookie). Without a registration every account may hold a session.
  - `AddBlueprintCookieSessions(Action<CookieBuilder>)`: turns the Identity application cookie into the session cookie. The callback shapes the cookie (name, `SameSite`, secure policy); the package then applies its fixed behaviour: HTTP-only, the ticket store, the lifetime from `SessionExpirationHours` with sliding expiration, a persistent cookie, 401/403 instead of redirects, and no Identity security-stamp validation. Cookie event handlers the application assigns keep running before the package's; a scheme that sets `EventsType` or overrides those events fails when its options are built.
  - `AddBlueprintSessionInfrastructure` takes an optional `Action<BlueprintSessionOptions>`: `ClientIpHeader` (the edge-stamped client address header) and `ExpiredSessionCleanup` (`Enabled`, `Interval`).
  
  **Behaviour change:** The ticket store tracks every session it stores: it writes the ticket, then creates the tracking row with the account, tenant, expiry, user agent and client address of the signing-in request. A sign-in that cannot record its session fails and leaves no ticket. Renewal brings the row in line with the ticket — expiry and tenant. A sign-in on a request that still carries a live session cookie reuses that cookie's session key; the row is then attributed to the signing-in account, with its sign-in time, user agent and address, so revoking that account ends the session and revoking the previous account does not.
  
  **Behaviour change:** The client address recorded for a session follows the rule the package's rate limiting uses (`ClientAddressHttpContextExtensions.TryGetClientAddress`, which `RateLimiterExtensions.TryGetClientPartitionKey` now calls): the configured `ClientIpHeader` when present and parseable, otherwise the connection's remote address.
  
  **Behaviour change:** The session lifetime comes from `Blueprint:Security:SessionExpirationHours` alone: `AddBlueprintDefaultValidatedOptions` sets `BlueprintSessionOptions.Lifetime` from it, the cookie's `ExpireTimeSpan` follows, and a ticket that reaches the store without an expiry gets it too. Sign-in code no longer sets `ExpiresUtc` or `IsPersistent`. Startup fails when the lifetime is shorter than one second.
  
  **Behaviour change:** A hosted job deletes the tracking rows of expired sessions, by default once an hour. Each pass deletes in batches by the existing `ExpiresAt` index, so every replica can run it without coordination. Switch it off with `ExpiredSessionCleanup.Enabled = false`.
  
  **Behaviour change:** Revoking intents keep removing the remaining cached tickets when one removal fails, keep the tracking row of a session whose ticket could not be removed, and still call the participants. A sign-out removes the cached ticket before it deletes the tracking row, and once more after it, so a sign-out whose ticket removal fails leaves the session tracked. A claims refresh that races a revocation removes the tickets it rewrote for sessions whose rows are gone. A claims refresh or tenant switch leaves alone a ticket that by then authenticates another account. A sliding renewal on a request that switched the caller's tenant or changed its own account's claims keeps that change.
  
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
    - `RevokeMySessionEndpoint`: the `ActiveSessions` ownership lookup and `RevokeSessionAsync` become `RevokeMySessionAsync(req.SessionId)`; map `false` to its 404 and `true` to its 204, and drop the `ApplicationDbContext` dependency.
  - Optionally register an `ISessionEligibility` (for example: a disabled or locked-out account may not hold a session) and `ISessionRevocationParticipant`s for the application's own credentials.
  - Tests: the package now covers what `SessionManagerTests`, `SessionLifetimeConfigurationTests` and `SwitchTenantSessionTargetingTests` checked; keep one HTTP test per behaviour the application relies on.

- [#60](https://github.com/neolution-ch/Csag.Blueprint/pull/60) [`4654e33`](https://github.com/neolution-ch/Csag.Blueprint/commit/4654e333330f260111ca0fdd3e06656af4afab14) Thanks [@neotrow](https://github.com/neotrow)! - Serve table view allowed values with human-readable, localizable labels
  
  Enum columns sent their raw member names (`CreditCard`, `FourSeater`) as the only text a frontend had for its filter options, and there was no way to translate them. Each allowed value now carries a label next to the value that filters match on.
  
  **Breaking:** `TableViewColumnMetadata.AllowedValues` is now `IReadOnlyList<TableViewAllowedValue>?` instead of `IEnumerable<string>?`. On the wire each entry is `{ "value": "CreditCard", "label": "Credit card" }` rather than a bare string. Filtering is unchanged: `Enum` and `In` filters still match on `value`.
  
  **New:** `TableViewAllowedValue` (Csag.Blueprint.Application), an immutable record with `Value`, `Label`, and two members that are never serialized: `LabelKey`, a translation key resolved at request time like `DisplayNameKey`, and `IsLabelKeyOptional`, which marks a key whose missing translation is expected.
  
  **Behaviour change:** Enum columns derive one allowed value per member: the member name as `Value`, the humanized member name as `Label` (`CreditCard` → "Credit card", `SMSCode` → "SMS code", `IBAN` → "IBAN", `Top10Items` → "Top 10 items"), and `Enums.{EnumTypeName}.{MemberName}` as an optional `LabelKey`. An application translates a label by adding that key to its translation catalog (for example a nested `Enums.PaymentMethod` class in `TranslationDefaults`); a member without a translation keeps the humanized label, and nothing is logged. Keys use the enum's simple type name, so two enums with the same name share keys.
  
  **New:** `TableViewColumnDefinition` gains `WithAllowedValues(IEnumerable<TableViewAllowedValue>)` for full control over values, labels and keys, and `WithValueLabelKeyPrefix(string prefix)`, which sets every current allowed value's key to `{prefix}.{Value}`. Keys set either way are expected to exist: a missing one is logged as a warning and the definition's label is served, as for display names. `WithValueLabelKeyPrefix` throws `InvalidOperationException` on a column without allowed values. `WithAllowedValues(IEnumerable<string>)` and `Filterable(operator, allowedValues)` keep taking plain strings, served with the string itself as the label and no key; `WithAllowedValues(IEnumerable<string>)` now rejects `null`.
  
  **Behaviour change:** `StringLocalizerTableViewMetadataLocalizer` resolves allowed-value label keys along with display names and descriptions. A column whose labels change is returned as a copy with a new allowed-values list; the definition's list and values are never modified.
  
  **Behaviour change:** `TableViewExcelExporter` writes a text or enum cell whose value matches one of the column's allowed values as that value's label, so exported enum columns show the same (localized) text as the table.
  
  Consumer migration: frontends read `allowedValues[i].value` for filter input and `allowedValues[i].label` for display, and regenerate any API client types. Server code that read `AllowedValues` as strings selects `.Value`. To translate enum labels, add `Enums.{EnumType}.{Member}` keys to the translation catalog, or point a column at existing keys with `WithValueLabelKeyPrefix`.

### Patch Changes

- [#56](https://github.com/neolution-ch/Csag.Blueprint/pull/56) [`11189d5`](https://github.com/neolution-ch/Csag.Blueprint/commit/11189d5d69415a20dc9f2cc787656f61ae3d903e) Thanks [@neotrow](https://github.com/neotrow)! - Write session profile claims for an account without an email address
  
  `UserClaimsHelper.SetUserProfileClaims` threw for a null or empty `IUserProfileClaimsSource.Email`, although the contract declares it nullable, so building or refreshing the claims of such an account failed. It now writes the identifier, name and preference claims and leaves the email claim out, removing one a ticket carried from before.

## 0.4.0

### Patch Changes

- [#48](https://github.com/neolution-ch/Csag.Blueprint/pull/48) [`1e4037d`](https://github.com/neolution-ch/Csag.Blueprint/commit/1e4037d1733ed52f70b7851f136c561ee5e48674) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 18 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | Audit.EntityFramework.Core | 32.3.1 | 33.0.0 | 🔴 major |
  | Audit.NET | 32.3.1 | 33.0.0 | 🔴 major |
  | Audit.NET.SqlServer | 32.3.1 | 33.0.0 | 🔴 major |
  | Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Data.SqlClient | 7.0.2 | 7.0.3 | 🟢 patch |
  | Microsoft.EntityFrameworkCore | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.EntityFrameworkCore.InMemory | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.EntityFrameworkCore.SqlServer | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Extensions.Caching.SqlServer | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Extensions.TimeProvider.Testing | 10.9.0 | 10.10.0 | 🟡 minor |
  | Microsoft.NET.Test.Sdk | 18.7.0 | 18.10.1 | 🟡 minor |
  | NLog.Web.AspNetCore | 6.2.0 | 6.2.1 | 🟢 patch |
  | Testcontainers.MsSql | 4.14.0 | 4.15.0 | 🟡 minor |
  | xunit.runner.visualstudio | 3.1.5 | 4.0.0 | 🔴 major |
  | xunit.v3.mtp-off | 4.0.0 | 4.0.1 | 🟢 patch |

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
