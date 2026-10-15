---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Own service-account credentials in the packages; applications state intents through `IServiceAccountCredentials` and `IServiceAccountSessionLifecycle`

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
