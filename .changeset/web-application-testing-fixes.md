---
"@neolution-ch/csag-blueprint-web": minor
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-testing": minor
---

Web, Application, and Testing fixes:

- `SecuritySettingsValidator` and `LocalizationOptionsValidator` report proper validation errors instead of throwing `NullReferenceException` when `CorsPolicies` / `SupportedLanguages` are null; `LocalizationOptions.TranslationCacheL1ExpirationMinutes` is now validated (must be greater than 0).
- `AddConfiguredCors` fails fast at wiring time with a clear `InvalidOperationException` for a wildcard origin mixed with explicit origins, and for the wildcard-plus-`AllowCredentials` combination — both previously surfaced late or not at all.
- `TenantMiddleware` clears any pre-existing ambient tenant before invoking downstream when the resolver yields no tenant, so stale ambient state never flows into request handling.
- `JwtSettingsValidator` no longer requires `SigningKey` to be present (generation-mode startup has no key; presence is enforced by the host at runtime as documented) — a key that *is* provided must still be at least 32 characters.
- TableView filters: numeric ranges support negative bounds (`"-5-10"` parses as -5..10), undefined enum values are rejected instead of silently matching nothing, `Equals` works on boolean columns, and `Filterable()` no longer wipes the auto-derived enum `allowedValues` from column metadata when called without an explicit list.
- `MigrationBuilderExtensions.SeedTranslation(s)` normalizes language codes to canonical lowercase so seeded rows always match translation lookups.
- `MsSqlTestContainerOrchestrator` no longer contacts the Docker daemon at construction time (the container is built in `StartAsync`), and the missing-`Initial Catalog` error no longer embeds the connection string (which contains the SA password). `ShouldHaveStatusCodeAsync` accepts an optional `CancellationToken` — binary-breaking for assemblies compiled against the previous version; recompile against this one.
