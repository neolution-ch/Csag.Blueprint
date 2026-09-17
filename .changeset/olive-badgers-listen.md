---
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Apply the blueprint model conventions after the model is complete

`BlueprintDbContext` applied its contract-driven conventions (multi-tenancy, contract constraints,
localized texts, soft-delete filtering, sequential `Guid` keys) inline in `OnModelCreating`, so they
only saw the entity types discovered up to that point. An application registering a type **after**
its `base.OnModelCreating` call — the usual shape of `ApplyConfigurationsFromAssembly`, owned types
and join entities, none of which need a `DbSet` — silently received none of them. For
`ISoftDeletable` that meant no global query filter at all, so soft-deleted rows came back in every
query.

The conventions now run from `BlueprintModelCustomizer`, an `IModelCustomizer` registered by
`BlueprintDbContext.OnConfiguring`, which applies them once `OnModelCreating` has run in full.
Registration order no longer matters.

The `Configure*` extension methods are unchanged and remain the entry point for contexts that do not
derive from `BlueprintDbContext`.

**Two things to check when upgrading:** a context overriding `OnConfiguring` must call
`base.OnConfiguring`, or no conventions are applied; and a context replacing `IModelCustomizer`
should derive from `BlueprintModelCustomizer` rather than `ModelCustomizer`. Entities that were
previously missed now gain a soft-delete filter, a `DeletedAt` index and a `NEWSEQUENTIALID()` key
default, which produces a migration.
