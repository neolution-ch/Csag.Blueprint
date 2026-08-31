---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Add entity contracts for soft deletion, active ranges, internal names and localized texts, with named global query filters

New `Csag.Blueprint.Domain` contracts: `ISoftDeletable`, `IHasActiveRange`, `IHasInternalName`,
`ILocalizedText` and `IHasLocalizedTexts<T>`.

`Csag.Blueprint.Infrastructure` gains the matching model conventions
(`ConfigureEntityFiltering`, `ConfigureLocalizedTextConventions`, `ConfigureContractConstraints`)
and query extensions (`WhereNotDeleted`, `WhereActiveNow`, `IncludeCurrentLanguageText`, …).
`BlueprintDbContext` applies the localized text and entity filtering conventions automatically.

Both blueprint-owned global query filters are now **named** (`BlueprintQueryFilters.Tenant` and
`BlueprintQueryFilters.SoftDelete`), so a query can opt out of one without losing the other. Use
`IgnoreSoftDeleteFilter()` / `OnlySoftDeleted()` to reach soft-deleted rows instead of the
parameterless `IgnoreQueryFilters()`, which also disables tenant isolation.

Consuming applications need their own EF Core migration for the columns and indexes the new
contracts introduce on entities that adopt them.
