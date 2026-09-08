# @neolution-ch/csag-blueprint-web

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
