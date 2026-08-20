# Test-suite follow-ups

Issues surfaced while building the unit-test layer (2026-08-20). Every behavioral finding is **pinned as current behavior** by a named test — when a fix lands, the pinning test must be updated in the same PR, and package changes need a changeset.

## P1 — behavior bugs worth a fix PR

1. **Global tenant query filter: the `HasValue` guard is dead code.**
   `MultiTenancyModelBuilderExtensions` builds the filter `CurrentTenantId.HasValue && TenantId == CurrentTenantId.Value`, but EF funcletizes `CurrentTenantId.Value` into an eagerly-evaluated query parameter, so any query over a tenant-owned set with no ambient tenant throws `InvalidOperationException("Nullable object must have a value")` instead of returning empty. Fail-closed by accident. Decide the intended contract (explicit fail-closed exception with a clear message, or empty results) and encode it.
   Pinned: `MultiTenancyModelBuilderExtensionsTests.QueryFilter_WithNoAmbientTenant_ThrowsOnQuery`.

2. **`SessionClaimsHelper.ApplySessionClaims` keeps the previous tenant's claim on a tenant-less rebuild.**
   With `tenantId == null` the existing `TenantId` claim is never removed, contradicting the method's own doc comment ("a tenant-less session carries none").
   Pinned: `SessionClaimsHelperTests.ApplySessionClaims_WithoutTenant_LeavesExistingTenantClaimInPlace`.

3. **`UserClaimsHelper.ReplaceClaim`/`ReplaceOptionalClaim` remove only the first duplicate claim** before adding the new value — inconsistent with `AuthorizationClaimsHelper`/`TenantClaimsHelper`, which remove all matches; a refreshed identity can keep a stale duplicate.
   Pinned: `UserClaimsHelperTests.SetUserProfileClaims_WithDuplicateExistingClaims_RemovesOnlyTheFirstDuplicate`.

4. **`SecuritySettingsValidator` throws `NullReferenceException`** when `DefaultCorsPolicy` is set and `CorsPolicies` is null (missing null guard before `ContainsKey`) instead of reporting the validation error.
   Pinned: `SecuritySettingsValidatorTests.Validate_DefaultCorsPolicySetWithNullCorsPolicies_Throws`.

5. **`LocalizationOptionsValidator` throws `NullReferenceException`** on a null `SupportedLanguages` list instead of surfacing the `NotEmpty` error.
   Pinned: `LocalizationOptionsValidatorTests.Validate_NullSupportedLanguages_Throws`.

6. **`MsSqlTestContainerOrchestrator.CreateSnapshotAsync` embeds the full connection string — including the SA password — in an exception message** (missing `Initial Catalog` path), risking credential leakage into test logs and CI output.

7. **CORS policy building is lenient in two ways** (`CorsBuilderExtensions.AddConfiguredCors`): a wildcard mixed with explicit origins (`"*;https://app.example.com"`) bypasses the lone-`"*"` `AllowAnyOrigin` branch and is passed literally to `WithOrigins` while `CorsPolicy.AllowAnyOrigin` still reports true; and the invalid wildcard-plus-`AllowCredentials` combination only fails when `CorsOptions` is first resolved, not at startup wiring time.
   Pinned: `CorsBuilderExtensionsTests` (wildcard-among-origins and wildcard-with-credentials tests).

## P1 — found by the integration suite

22. **Runtime OpenAPI document returns 500 whenever the first NSwag-generated operation carries a FastEndpoints validator** (order-dependent). FastEndpoints registers its `ProblemDetails` schema first, so `Mvc.ProblemDetails` becomes `ProblemDetails2`; for every operation after the first, `ProblemDetailsOperationProcessor` wraps a reference-wrapper schema in another reference, and `UnifiedProblemDetailsDocumentProcessor.ReplaceSchemaReferences` only rewrites direct references before removing `ProblemDetails2` — chained references then terminate at the removed schema and `ToJson()` throws. The web repo's endpoint ordering happens to dodge this; **every downstream app is one endpoint-ordering change away from a broken `/swagger/v1/swagger.json`**. Fix options: resolve the registered schema once in the operation processor, or make `ReplaceSchemaReferences` follow reference chains, or rebuild the duplicate in place instead of removing it. Files: `packages/Csag.Blueprint.Web/Swagger/ProblemDetailsOperationProcessor.cs`, `UnifiedProblemDetailsDocumentProcessor.cs`.
    Pinned: `SwaggerEndpointTests.SwaggerJson_WhenEnabled_CurrentlyFailsSerializationWithDanglingProblemDetailsRefsAsync` (flip to a 200 assertion when fixed).

23. **Enum column `AllowedValues` never reaches clients.** `TableViewDefinition.Column()` auto-derives `AllowedValues` for enum DTO properties, but `TableViewColumnDefinition.Filterable(operator)` unconditionally overwrites `metadata.AllowedValues` with its optional parameter (null when omitted) — and every real definition calls `Filterable` after `Column`, so metadata serves `allowedValues: null` and the auto-derivation is dead code. Fix: only overwrite when the parameter is non-null, or re-derive for enum columns. Extends item 9(b).
    Pinned: `TableViewTests.MetadataEndpoint_ReturnsAllColumnDefinitionsAsync`.

## P2 — contract mismatches and API robustness

8. **`JwtSettingsValidator` doc/code mismatch:** the class doc states `SigningKey` is *not* validated here (deferred to the host, gated behind generation mode), but the rules enforce `NotEmpty` + `MinimumLength(32)` — per the doc's own rationale this could fail generation-mode startup. Pinned in `JwtSettingsValidatorTests`.
9. **TableView filter expressions** (`TableViewDefinition`): (a) `BuildRangeExpression` splits on `-`, so a negative minimum (`"-5-10"`) is unrepresentable and yields null; (b) `BuildEnumExpression` uses `Enum.TryParse`, which accepts undefined numeric values (`"999"` matches nothing rather than being rejected) and `Metadata.AllowedValues` is never enforced server-side; (c) `BuildEqualsExpression` has no boolean branch, so `Filterable(Equals)` on a bool column silently yields no filter. Pinned in the Application TableView filter tests.
10. **`TenantMiddleware`** does not clear a pre-existing ambient `TenantContext` value when the resolver returns null, so a value from the calling execution context flows downstream (benign under real ASP.NET Core hosting; relevant for background-work reuse). Pinned in `TenantMiddlewareTests`.
11. **`TranslationProvider`** compares the requested language to the default language with `OrdinalIgnoreCase` but the EF query uses ordinary equality — on a case-sensitive DB collation a case-variant request can skip the fallback and silently yield code defaults.
12. **`MsSqlTestContainerOrchestrator` is unconstructible without Docker:** the constructor eagerly calls `MsSqlBuilder(image).Build()`, which pings the Docker daemon. Deferring `Build()` to `StartAsync` would make the class constructible anywhere and its guard clauses testable.
13. **`ShouldHaveStatusCodeAsync`** reads the response body without a `CancellationToken` overload.
14. **`LocalizationOptions.TranslationCacheL1ExpirationMinutes` has no validation rule** — zero and negative values pass.

## P2 — TranslationKeysGenerator (from the generator test round)

15. A `TranslationDefaults` class in the global namespace emits `namespace <global namespace>;` — invalid C#; generated sources cannot compile.
16. The consumer's `TranslationDefaults` class must be `partial`, but a non-partial class fails the consuming build with CS0260 and no generator diagnostic explains why.
17. Multiple `TranslationDefaults` classes across namespaces are silently merged into the first class's namespace with first-wins duplicate-key resolution; a root-level const colliding with a same-named nested class would emit uncompilable output with no diagnostic.
18. Entries are sorted with the culture-sensitive default comparer, so non-ASCII key ordering could vary by host culture; use `StringComparer.Ordinal`.

## P3 — process and housekeeping

19. **SSH.NET 2025.1.0 advisory (NU1903, high severity, GHSA-q939-rpr3-3284)** — transitive via `Csag.Blueprint.Testing`'s Testcontainers chain; bump when an updated dependency chain is available.
20. **CI shape:** once integration tests land, consider splitting `dotnet test` into unit and integration jobs (integration needs Docker and container startup time; unit feedback stays fast).
21. Downstream (`csag-blueprint-web`): delete/thin the ported test files only after these packages are released with the tests running in CI and the web repo's pin is bumped; keep the consumer smoke tests per `docs/architecture/PACKAGES.md`. Move `parseServerErrors.test.ts` into `src/packages/react-form-kit/__tests__/` before any npm extraction.
