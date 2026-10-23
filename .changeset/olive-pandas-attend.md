---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Add entity contracts for soft deletion, active ranges, internal names and localized texts, with named global query filters

New `Csag.Blueprint.Domain` contracts: `ISoftDeletable`, `IHasActiveRange`, `IHasInternalName`,
`ILocalizedText` and `IHasLocalizedTexts<T>`.

`Csag.Blueprint.Infrastructure` gains the matching model conventions (`ConfigureEntityFiltering`,
`ConfigureLocalizedTextConventions`, `ConfigureContractConstraints`, `ConfigureGuidPrimaryKeyDefaults`)
and query extensions (`WhereNotDeleted`, `WhereActiveNow`, `IncludeCurrentLanguageText`,
`SelectWithCurrentLanguageText`, …). `BlueprintDbContext` applies the conventions automatically.

- **Named query filters.** Both blueprint-owned global query filters are registered under a name
  (`BlueprintQueryFilters.Tenant` and `BlueprintQueryFilters.SoftDelete`), so a query can opt out of
  one without losing the other. Use `IgnoreSoftDeleteFilter()` / `OnlySoftDeleted()` to reach
  soft-deleted rows; the parameterless `IgnoreQueryFilters()` also disables tenant isolation.
- **Soft deletion and active ranges.** `ISoftDeletable` entities get the soft-delete filter and an
  `IX_{table}_DeletedAt` index. `IHasActiveRange` deliberately gets no filter, only a composite
  `IX_{table}_ActiveRange` index: the point in time an active range is evaluated against is the
  caller's choice, so use `WhereActiveNow()` / `WhereActiveAt()` / `WhereActiveInRange()`.
- **Inheritance.** The filters are registered on the root of an inheritance hierarchy, which is the
  only place EF Core allows them, and derived types inherit them. A hierarchy that adopts
  `ISoftDeletable` or `IMustHaveTenant` *below* its root cannot be filtered by EF Core and fails at
  model build with an explanatory message.
- **Localized texts.** `ConfigureLocalizedTextConventions` cascades deletes from an entity to its
  texts and adds a per-language unique index, using whatever foreign key the relationship has.
  `IncludeCurrentLanguageText`, `WhereHasCurrentLanguageText`, `CurrentLanguageTextExpression` and
  `SelectWithCurrentLanguageText` rank texts the same way, inside the EF Core query: exact current
  language, then the same language in another region (including the bare code), then the exact
  fallback, then the fallback's language. These, and the single-language helpers
  (`IncludeTextsForLanguage`, `WhereHasTextForLanguage`, …), compare language codes
  case-insensitively on every provider.
- **Language providers.** `ICurrentLanguageProvider` supplies the current and fallback language.
  `DefaultLanguageProvider` reads `CultureInfo.CurrentUICulture` and falls back to a configured code
  for the invariant culture; `ExplicitLanguageProvider` covers call sites that already know the
  language, such as an endpoint taking it as a parameter.
- **Sequential keys on SQL Server.** `ConfigureGuidPrimaryKeyDefaults` gives every single-column
  `Guid` primary key a `NEWSEQUENTIALID()` column default, for inserts that bypass EF Core. EF Core
  keeps assigning keys client-side on `Add`, so an added entity's key is usable before `SaveChanges`.
  Keys that declare a default or `ValueGeneratedNever()`, and keys that are also foreign keys (such as
  an owned type's), are left alone. `BlueprintDbContext` applies it only on SQL Server, where the
  function exists.

**When upgrading:** generate an EF Core migration. On SQL Server, every single-column `Guid` primary
key gains the `NEWSEQUENTIALID()` default, and entities adopting the new contracts gain their columns
and indexes.
