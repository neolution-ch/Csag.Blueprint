---
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Infrastructure behavior fixes:

- The global tenant query filter now compares `TenantId` against the ambient tenant in lifted nullable form (`e.TenantId == context.CurrentTenantId`). With no ambient tenant, tenant-owned queries deterministically return no rows (fail-closed by empty result) instead of throwing `InvalidOperationException("Nullable object must have a value")`. Callers that need cross-tenant access must use `IgnoreQueryFilters()` deliberately — including idempotent seeders, which must set the ambient tenant before existence checks.
- `SessionClaimsHelper.ApplySessionClaims` removes any existing `TenantId` claim when rebuilding a session without a tenant, so a tenant-less session carries no tenant claim (matching its documented contract).
- `UserClaimsHelper` profile claim replacement removes all existing claims of a type before adding the fresh value, consistent with the role/permission/tenant helpers; stale duplicates can no longer survive a ticket refresh.
- `TranslationProvider` and `TranslationCacheInvalidator` normalize language codes to canonical lowercase for cache keys, database lookups, and the requested-vs-default-language comparison, so translation resolution no longer depends on caller casing or database collation. Translation rows are expected to store the canonical lowercase code.
