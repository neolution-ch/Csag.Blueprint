---
"@neolution-ch/csag-blueprint-infrastructure": minor
---

Apply the blueprint model conventions once the model is complete, below the application's own configuration

`BlueprintDbContext` used to apply multi-tenancy inline in `OnModelCreating`, so it only reached the
entity types discovered at that point. An `IMustHaveTenant` entity your context registered *after*
its `base.OnModelCreating` call without a `DbSet`, as `ApplyConfigurationsFromAssembly` often does,
got no tenant filter at all, and its rows were visible to every tenant.

All blueprint conventions (multi-tenancy, contract constraints, localized texts, soft-delete
filtering, sequential `Guid` keys) now run from `BlueprintModelFinalizingConvention`, registered in
`ConfigureConventions`, once EF Core has finished building the model. Every entity type is covered,
however it entered the model.

The conventions are applied at EF Core's convention level, so your own configuration wins even
though they run last. That applies to explicit fluent configuration and data annotations, and to
the `Configure*` extension methods when you call them directly. A tenant relationship you map yourself
keeps its navigation and delete behavior, a different `InternalName` length or index name is kept,
and so on. Owned types are supported; they carry no filters of their own, as EF Core requires.

**When upgrading:**

- A context overriding `ConfigureConventions` must call `base.ConfigureConventions`, or no blueprint
  conventions are applied, tenant isolation included.
- An unnamed query filter of your own on an `IMustHaveTenant` entity now makes the model fail to
  build, because EF Core does not allow unnamed and named filters on the same entity. Register it
  under a name (`HasQueryFilter("Archived", e => !e.Archived)`). Previously such a filter replaced the
  tenant filter without warning. The `Tenant` and `SoftDelete` names themselves are reserved for
  the blueprint's filters, so a filter of yours registered under one of them fails the build too.
- Generate an EF Core migration. Tenant-owned entities registered without a `DbSet` gain their
  `TenantId` index and tenant foreign key. An entity with a `Tenant` navigation now shares the single
  `TenantId` foreign key instead of getting a second, shadow `TenantId1` one, so the migration drops
  the `TenantId1` column.
