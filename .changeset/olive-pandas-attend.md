---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Add entity contracts for soft deletion, active ranges, internal names and localized texts, with named global query filters

New `Csag.Blueprint.Domain` contracts: `ISoftDeletable`, `IHasActiveRange`, `IHasInternalName`,
`ILocalizedText` and `IHasLocalizedTexts<T>`.

`Csag.Blueprint.Infrastructure` gains the matching model conventions
(`ConfigureEntityFiltering`, `ConfigureLocalizedTextConventions`, `ConfigureContractConstraints`,
`ConfigureGuidPrimaryKeyDefaults`) and query extensions (`WhereNotDeleted`, `WhereActiveNow`,
`IncludeCurrentLanguageText`, …). `BlueprintDbContext` applies all of them automatically.

Both blueprint-owned global query filters are now **named** (`BlueprintQueryFilters.Tenant` and
`BlueprintQueryFilters.SoftDelete`), so a query can opt out of one without losing the other. Use
`IgnoreSoftDeleteFilter()` / `OnlySoftDeleted()` to reach soft-deleted rows instead of the
parameterless `IgnoreQueryFilters()`, which also disables tenant isolation.

`SelectWithCurrentLanguageText()` and `CurrentLanguageTextExpression()` resolve the localized text
fallback **inside** an EF Core query, so applications no longer have to hand-write the ranking
inline to keep it translatable. The ranking is now restricted to the four supported tiers (exact
current language → same language in another region, including the bare language code → exact
fallback → same language as the fallback): an entity that only has texts in an unrelated language
now yields `null` instead of an arbitrary translation. `WhereHasCurrentLanguageText()` follows the
same language matching and no longer requires an exact language-code match, so a `de` text now
satisfies a current language of `de-CH`.

`DefaultLanguageProvider` now actually reads `CultureInfo.CurrentUICulture`, as its documentation
always claimed, and falls back to a configured language code for the invariant culture. Its
two-argument constructor is replaced by a single fallback code. The new `ExplicitLanguageProvider`
covers call sites that already know the language, such as an endpoint taking it as a parameter.

**Removed:** `StaticLocalizationExtensions` (ambient service-locator state; register an
`ICurrentLanguageProvider` in DI instead) and `QueryStringExtensions.StartsWithQuery` (a custom
method EF Core could not translate). `ConfigureLocalizedTextIndexes` is gone — its index duplicated
one already created by `ConfigureLocalizedTextConventions`.

Consuming applications need their own EF Core migration: the new contracts add columns to entities
that adopt them, `ConfigureEntityFiltering` now also creates `IX_{table}_DeletedAt` and the
composite `IX_{table}_ActiveRange`, and every single-column `Guid` primary key in the model gets a
`NEWSEQUENTIALID()` default.
