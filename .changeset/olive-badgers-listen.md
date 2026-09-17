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

The conventions now run from `BlueprintModelFinalizingConvention`, registered in
`ConfigureConventions`, which applies them once EF Core has finished building the model.
Registration order no longer matters.

The `Configure*` extension methods are unchanged and remain the entry point for contexts that do not
derive from `BlueprintDbContext`.

**When upgrading:** a context overriding `ConfigureConventions` must call
`base.ConfigureConventions`, or no conventions are applied. Entity types that were previously missed
now gain a soft-delete filter, a `DeletedAt` index and a `NEWSEQUENTIALID()` key default, which
produces a migration.

`ConfigureGuidPrimaryKeyDefaults` is applied only when the context runs on SQL Server.
`NEWSEQUENTIALID()` does not exist elsewhere, and other providers fail late rather than loudly:
SQLite accepts the generated DDL and then rejects every insert with a `DbUpdateException`.

The localized-text queries compare language codes case-insensitively on every provider. SQL equality
follows the database collation, so under a case-sensitive one — SQLite's default, and a valid SQL
Server choice — a stored `DE` resolved through the in-memory helper but to `null` in SQL. Language
tags are case-insensitive by definition, so the query side was the one that was wrong.
