# @neolution-ch/csag-blueprint-web

## 0.7.0

### Minor Changes

- [#44](https://github.com/neolution-ch/Csag.Blueprint/pull/44) [`dbb4310`](https://github.com/neolution-ch/Csag.Blueprint/commit/dbb4310799f3cdf954e2f9247961670cc32a1652) Thanks [@neotob](https://github.com/neotob)! - Stop the culture fallback from swapping one region for another
  
  `CultureNormalizationHelper` previously fell back on the two-letter language for **any** request,
  so `fr-FR` resolved to a supported `fr-CH`. The language fallback now narrows but never swaps
  regions: it still applies when either side is a bare language code — `de` resolves to a supported
  `de-CH`, and `en-GB` resolves to a supported bare `en` — but two region-qualified tags must match
  exactly.
  
  This is a behaviour change: requests for a regional variant you do not support now fall through to
  the default language instead of silently being served a different region's content.

## 0.6.0

### Minor Changes

- [#65](https://github.com/neolution-ch/Csag.Blueprint/pull/65) [`57c3c85`](https://github.com/neolution-ch/Csag.Blueprint/commit/57c3c8554bbf10e84bce1c674806c0be214b0f99) Thanks [@neotrow](https://github.com/neotrow)! - Let applications decide which tenant an entity-change audit event is recorded under
  
  `ConfigureBlueprintAuditLogging` records every EF audit event under the ambient `TenantContext.Current`, the actor's active tenant. That is not always the tenant the change belongs to: a platform administrator who changes a global row while a tenant is active records the change under that tenant.
  
  **New:** `BlueprintAuditOptions.EntityChangeTenantResolver`, an `AuditTenantResolver` delegate `(HttpContext? httpContext, Guid? activeTenantId) => Guid?`. It is called once per `SaveChanges`, with the request the change was saved in (null outside one) and the active tenant, and returns the tenant to write to the `TenantId` column, or null for none. Left unset, the active tenant is recorded, as before. HTTP audit events are not affected.
  
  Consumer migration: none.

- [#64](https://github.com/neolution-ch/Csag.Blueprint/pull/64) [`662d1b5`](https://github.com/neolution-ch/Csag.Blueprint/commit/662d1b5f439af6d687898c5b96f3a67a4c764a2e) Thanks [@neotrow](https://github.com/neotrow)! - Table view exports validate pagination and cap unpaginated exports at `MaxExportRows`
  
  `TableViewExportEndpointBase` accepted any `Page` and `PageSize`, and an export without them read every row matching its filters into one workbook, however many there were.
  
  **New:** `TableViewExportRequestValidator` (Csag.Blueprint.Web) applies to every endpoint taking a `TableViewExportRequest`. `Page` and `PageSize` are set both or neither; when set, `Page` is at least 1 and `PageSize` is between 1 and 100, as for `TableViewDataRequest`. Sort columns follow the same rules as on the data request.
  
  **New:** `TableViewExportEndpointBase.MaxExportRows`, a `protected virtual int`, default `10_000`. An export without a page that matches more rows than that is refused with `400 Bad Request` and a ProblemDetails body naming the cap; it is never truncated. Override it per endpoint, like `ExportFileName`.
  
  **Behaviour change:** the base endpoint reads an unpaginated export through `ITableViewExecutor.ExecuteAsync` as one page of `MaxExportRows + 1` rows, instead of `ExecuteWithoutPaginationAsync`; the extra row detects a match the separate count read missed. `ExecuteWithoutPaginationAsync` remains on the interface.
  
  Consumer migration: a client that sent `Page` without `PageSize` (or the reverse), or a `PageSize` above 100, now gets `400` — send both, within the bounds, or neither. An endpoint whose tables legitimately export more than 10,000 rows overrides `MaxExportRows`. Frontends surface the `400` to the user, for example as a hint to narrow the filters.

### Patch Changes

- [#63](https://github.com/neolution-ch/Csag.Blueprint/pull/63) [`4d7a818`](https://github.com/neolution-ch/Csag.Blueprint/commit/4d7a818b97c31875a7555c4963b616de3e146288) Thanks [@neotrow](https://github.com/neotrow)! - Record audit entries as column values only, so field exclusions apply to the whole entry
  
  `ConfigureBlueprintAuditLogging` stored each EF audit entry with a serialized copy of the entity object next to its column values and changes. Audit.NET applies field exclusions (`ForEntity<T>(e => e.Ignore(...))`) to the column values and changes only, so the entity copy still carried the excluded fields, the standard ones (`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `ClientSecretHash`) and any an application adds through `BlueprintAuditOptions.EntityFieldConfigurator` alike. Entity objects are now left out (`IncludeEntityObjects(false)`), and an excluded field appears nowhere in the stored entry.
  
  **Behaviour change:** entries in `BlueprintAuditLogs.JsonData` no longer have an `Entity` property under `EntityFrameworkEvent.Entries[]`. `ColumnValues` and `Changes` are unchanged and carry every field that is not excluded.
  
  Consumer migration: code or queries that read `Entries[].Entity` read `Entries[].ColumnValues` instead. Rows written before this release still hold the entity copy; an application that wants the excluded fields gone from existing rows removes `Entity` from them in a data migration.

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

- [#56](https://github.com/neolution-ch/Csag.Blueprint/pull/56) [`11189d5`](https://github.com/neolution-ch/Csag.Blueprint/commit/11189d5d69415a20dc9f2cc787656f61ae3d903e) Thanks [@neotrow](https://github.com/neotrow)! - Reject control characters in the return address of a failed OpenID Connect round trip
  
  With `OAuth.FrontendBaseUrl` set, a failed provider round trip redirects to the challenge's `AuthenticationProperties.RedirectUri` when it is a local path. That check now uses `RedirectHttpResult.IsLocalUrl` (still requiring a leading `/`), so a return address carrying a control character such as CR or LF is treated as non-local and the user is sent to `FrontendBaseUrl` with `?error=external_auth_failed` instead.

## 0.4.0

### Minor Changes

- [#45](https://github.com/neolution-ch/Csag.Blueprint/pull/45) [`d61777b`](https://github.com/neolution-ch/Csag.Blueprint/commit/d61777b86ed9c8e917e1a69cb291699c4cdbb891) Thanks [@neotrow](https://github.com/neotrow)! - Route OpenID Connect callbacks through the frontend origin when `OAuth.FrontendBaseUrl` is set
  
  Behind the SPA's `/api/` proxy the API only sees the proxy's upstream host, so the OpenID Connect handler built its `redirect_uri` from that: on Cloud Run, the API's internal `run.app` address. Found on a deployed blueprint app, where Google refused the authorize request with `redirect_uri_mismatch`. Registering that address would not have helped: staging's API ingress is internal, and the challenge's correlation and nonce cookies live on the frontend host the browser used, so no callback on another host could validate.
  
  **Behaviour change:** With `OAuth.FrontendBaseUrl` set, every provider is now told to return to that origin plus the scheme's callback path (set in `OnRedirectToIdentityProvider` after any existing handler has run, so nothing undoes it; the handler reads the value back for the code exchange, so the authorize and token requests send the same one). The frontend origin forwards `/api/` to the API, so the callback and its cookies reach the handler. Register `<frontend origin><CallbackPath>` with each provider, e.g. `https://app.example.com/api/auth/signin-google`. Nothing is read from forwarded headers.
  
  **Breaking:** With `OAuth.FrontendBaseUrl` set, `OAuthSettingsValidator` rejects an enabled provider whose effective `CallbackPath` is not under `/api/`, naming the provider. Such a path lands on the SPA, not the API. The check is on the path as a proxy would route it: empty, `.` or `..` segments, percent-encoding and backslashes are rejected, so neither `/api/../signin-google` nor `/api/%2e%2e%2fsignin-google` passes. The same applies to a query or fragment, which the handler's match against the request path would never see. Move it under `/api/` and re-register it with the provider.
  
  **Breaking:** The default `CallbackPath` is now `/api/auth/signin-oidc/{scheme}` instead of `/signin-oidc/{scheme}`, so a provider relying on the default satisfies the rule above. Providers that set `CallbackPath` explicitly are unaffected; one relying on the default must re-register its redirect URI.
  
  **Behaviour change:** With `OAuth.FrontendBaseUrl` set, a failed provider round trip (the user cancels at the provider, a missing or expired correlation cookie) no longer throws `AuthenticationFailureException` into a 500. The failure is logged as a warning, the external cookie is cleared so an earlier attempt's identity cannot be judged in its place, and the user is redirected to the challenge's `AuthenticationProperties.RedirectUri` when it is a local path (the application's external callback, which answers a missing external login with its usual error redirect), otherwise to `FrontendBaseUrl` with `?error=external_auth_failed`. Both handlers are installed in a per-scheme post-configure step, so they wrap whatever the provider profile and any application `Configure<OpenIdConnectOptions>` put on the events, in either registration order: those handlers run first, and an `OnRemoteFailure` handler that handles the failure itself is left to do so. Some configurations would bypass both: a scheme that sets `EventsType` (its events come from DI), or whose events object overrides `RedirectToIdentityProvider` or `RemoteFailure` (the handler calls those methods, not the delegates). An application `Configure` could also move `OpenIdConnectOptions.CallbackPath` out of `/api/` after validation. With `FrontendBaseUrl` set, each of these now fails when the scheme's options are built, naming the provider.
  
  **Behaviour change:** The Entra profile installs its `OnTokenValidated` claim normalization in a per-scheme post-configure step (new `IOidcProviderProfile.PostConfigure`, a default no-op, so existing implementations keep compiling) and composes it with the handler already there, instead of replacing `OpenIdConnectEvents` during configuration. The normalization is what the external callback's email-trust gate relies on: it drops any `email_verified` that arrived in the token and stamps a trustworthy one. An application `Configure<OpenIdConnectOptions>` on an Entra scheme could previously discard it (registered before `AddBlueprintServices`, its events were replaced) or replace it (registered after), letting a token-supplied `email_verified` through. Now it runs in either order, first, so the application's handler sees the trusted claims. An Entra scheme whose events would skip it (one that sets `EventsType`, or whose events object overrides `TokenValidated`) now fails when the scheme's options are built, naming the provider, whether or not `FrontendBaseUrl` is set.
  
  New `OidcCallbackPaths` resolves each scheme's effective callback path (registration and validation both use it), exposes the proxied prefix, and checks a path against it (`IsUnderProxiedPrefix`).
  
  **Breaking:** `OAuthSettingsValidator` rejects an `OAuth.FrontendBaseUrl` without a host or carrying user-info, a query or a fragment. The redirect URI is built from its origin, which would have sent any user-info to the provider, and redirects append a path to it, which would have landed after a query or fragment.
  
  `OAuthSettings.FrontendBaseUrl` and `OAuthHelpers.BuildPostAuthRedirect` no longer describe external sign-in as completing on the API origin.
  
  Consumer migration: `csag-blueprint-web` carries the same behaviour as `FrontendRoutedOidcCallbackExtensions` (plus its own startup check). Delete that class, its registration and its tests when upgrading, and point `docs/security/EXTERNAL_OIDC.md` (the *Routing Through the Frontend Origin* section and the `CallbackPath` row, whose default and failure mode change) and the default-path comment in `PackageOAuthSettingsValidatorTests` at the package. Its explicit `/api/auth/signin-google` and `/api/auth/signin-microsoft` callback paths already satisfy the new rule.

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

- [#40](https://github.com/neolution-ch/Csag.Blueprint/pull/40) [`3892572`](https://github.com/neolution-ch/Csag.Blueprint/commit/389257242903cedf502f7ea2f4c622f67a3f9d3b) Thanks [@neotrow](https://github.com/neotrow)! - Add rate-limiting primitives for hosts wiring up `AddRateLimiter`:
  
  - `RateLimiterExtensions.TryGetClientPartitionKey` resolves the key identifying a caller, preferring an
    edge-stamped header and falling back to `Connection.RemoteIpAddress`. It reports failure rather than
    inventing a key, so a host can route an unidentifiable caller to `RateLimitPartition.GetNoLimiter`
    instead of collapsing every such caller into one shared bucket. The key is normalized: an IPv4-mapped
    IPv6 address collapses to IPv4, and a native IPv6 address is truncated to its `/64` prefix
    (`2001:db8:85a3:8d3::/64`), because a caller varying the source address inside one routed /64 would
    otherwise get a bucket of their own on each of 2^64 addresses.
  - `RateLimiterExtensions.UseBlueprintRejectionResponse` shapes the rejection: `429 Too Many Requests`
    (the framework default is `503 Service Unavailable`, which an upstream load balancer reads as a
    backend fault), an RFC 9457 ProblemDetails body carrying the correlation ID, and a `Retry-After`
    header when the limiter reports one. The delay is serialized as a `long`, and a lease reporting a
    negative delay writes no header at all: RFC 9110 `delay-seconds` is unsigned, and `0` would tell a
    client to retry immediately against the limiter that just rejected it. `TokenBucketRateLimiter` reaches
    both edges with ordinary options — it derives the value from an unchecked `ReplenishmentPeriod.Ticks`
    multiply, which exceeds `Int32` seconds well before it overflows `Int64` and goes negative.
  
  The `UseBlueprintSecurityHeaders` forwarded-headers documentation is corrected: `RemoteIpAddress` is the
  nearest hop, not the caller. Behind an edge that appends its own `X-Forwarded-For` entry — a Google
  external load balancer sends `<client>,<balancer>` — the rightmost entry that `ForwardLimit 1` reads is
  the balancer, and behind a further reverse proxy it is that proxy's egress address, one constant shared
  by every caller. Hosts partitioning a rate limiter, or recording a caller's address, must resolve the
  client from an edge-stamped header. Behavior is unchanged.
  
  That method's remarks now also state the deployment requirement it has always carried: it registers
  `ForwardedHeadersMiddleware` with `KnownProxies` and `KnownIPNetworks` empty, which turns off the
  known-proxy check, so the app must be reachable only through an edge that rewrites or appends
  `X-Forwarded-For` and `X-Forwarded-Proto`. A caller that can connect directly otherwise supplies both
  itself, and a forged `X-Forwarded-Proto: https` satisfies the HTTPS redirection and HSTS middleware.

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

## 0.2.0

### Minor Changes

- [#31](https://github.com/neolution-ch/Csag.Blueprint/pull/31) [`8bd90b6`](https://github.com/neolution-ch/Csag.Blueprint/commit/8bd90b60fdc4d43dcc6b6bf79c7b289cfc0f1bbf) Thanks [@neoscie](https://github.com/neoscie)! - Narrow HTTP request auditing to denied requests only, and register it automatically
  
  **Breaking change.** `HttpAuditMiddleware` no longer records an event for every HTTP request. It
  now records one event for a request whose final status code is 401 or 403 by default. GCP and
  Application Insights already capture general request data. So a request outside the audited set
  writes no event. The audit log now keeps only the EF Core events and the events for an audited HTTP
  request.
  
  An event keeps `EventType` (method and path), `StatusCode`, `UserId`, `TenantId`, `UserEmail`,
  `UserDisplayName`, `CorrelationId`, `UserAgent`, and the new `IpAddress`. Every event drops
  `HttpMethod`, `Url`, `DurationMs`, and `UserType`.
  
  The audited status codes are configurable through `HttpAuditOptions.AuditedStatusCodes`, 401 and 403
  by default. Register `services.Configure<HttpAuditOptions>(o => ...)` to audit a different set.
  
  **`app.UseBlueprintMiddleware()` now registers `HttpAuditMiddleware` itself, first, so that it always
  wraps authentication and authorization, and also the exception handler and status code pages.** This
  lets it audit a status code the exception handler itself produces, for example a 500 added to
  `AuditedStatusCodes`. An app must remove its own `app.UseMiddleware<HttpAuditMiddleware>()` call.
  Keeping it registers the middleware twice, which records two events per audited request.
  
  `ConfigureBlueprintAuditLogging` now configures both audit features, the EF Core events and the HTTP
  request events, from this one call. Both are on by default. Its configure callback can set
  `BlueprintAuditOptions.HttpAudit.Enabled = false` to turn HTTP request auditing off.

- [#27](https://github.com/neolution-ch/Csag.Blueprint/pull/27) [`92310d6`](https://github.com/neolution-ch/Csag.Blueprint/commit/92310d656e62d5006c5792b0899224d71e5986fa) Thanks [@neotrow](https://github.com/neotrow)! - Web, Application, and Testing fixes:
  
  - `SecuritySettingsValidator` and `LocalizationOptionsValidator` report proper validation errors instead of throwing `NullReferenceException` when `CorsPolicies` / `SupportedLanguages` are null; `LocalizationOptions.TranslationCacheL1ExpirationMinutes` is now validated (must be greater than 0).
  - `AddConfiguredCors` fails fast at wiring time with a clear `InvalidOperationException` for a wildcard origin mixed with explicit origins, and for the wildcard-plus-`AllowCredentials` combination — both previously surfaced late or not at all.
  - `TenantMiddleware` clears any pre-existing ambient tenant before invoking downstream when the resolver yields no tenant, so stale ambient state never flows into request handling.
  - `JwtSettingsValidator` no longer requires `SigningKey` to be present (generation-mode startup has no key; presence is enforced by the host at runtime as documented) — a key that *is* provided must still be at least 32 characters, measured after trimming, so a whitespace-only or whitespace-padded value cannot pass as key material.
  - TableView filters: numeric ranges support negative bounds (`"-5-10"` parses as -5..10), undefined enum values are rejected instead of silently matching nothing, `Equals` works on boolean columns, and `Filterable()` no longer wipes the auto-derived enum `allowedValues` from column metadata when called without an explicit list.
  - `MigrationBuilderExtensions.SeedTranslation(s)` normalizes language codes to canonical lowercase so seeded rows always match translation lookups.
  - `MsSqlTestContainerOrchestrator` no longer contacts the Docker daemon at construction time (the container is built in `StartAsync`), and the missing-`Initial Catalog` error no longer embeds the connection string (which contains the SA password). `ShouldHaveStatusCodeAsync` accepts an optional `CancellationToken` — binary-breaking for assemblies compiled against the previous version; recompile against this one.

### Patch Changes

- [#38](https://github.com/neolution-ch/Csag.Blueprint/pull/38) [`f913fee`](https://github.com/neolution-ch/Csag.Blueprint/commit/f913feef2951bf26564b5286225e29590f5efc77) Thanks [@neotrow](https://github.com/neotrow)! - Make the OpenAPI Problem Details unification independent of endpoint compile order
  
  Two CLR types reach the OpenAPI document under the same schema name:
  `Microsoft.AspNetCore.Mvc.ProblemDetails`, which `ProblemDetailsOperationProcessor` attaches to every
  operation, and FastEndpoints' own `ProblemDetails`, used for validation failures. NSwag gives one of
  them the bare name and suffixes the other `ProblemDetails2`. Which one wins depends on the order the
  schema generator first encounters them, which follows endpoint discovery order and therefore compile
  order.
  
  `UnifiedProblemDetailsDocumentProcessor` assumed a fixed winner. It removed `ProblemDetails2` after
  rewriting references, but the rewrite only walked Paths → Operations → Responses → Content. A
  reference reachable any other way survived and pointed at a definition that no longer existed, so
  `OpenApiDocument.ToJson()` threw *"Could not find the JSON path of a referenced schema"*.
  
  Because the trigger is compile order, this surfaced in consuming apps as a build that broke when an
  endpoint folder was renamed — the build-time spec export failed with no obvious connection to the
  rename.
  
  The processor now redirects alias references with a `JsonReferenceVisitorBase` walk over the whole
  document, so request bodies, parameters, nested properties, array items and composition keywords are
  covered too, and it folds every `ProblemDetails<N>` alias rather than only `ProblemDetails2`. The
  generated document is unchanged for consumers that were already working.

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
