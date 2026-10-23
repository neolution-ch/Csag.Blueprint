---
"@neolution-ch/csag-blueprint-web": patch
---

Record audit entries as column values only, so field exclusions apply to the whole entry

`ConfigureBlueprintAuditLogging` stored each EF audit entry with a serialized copy of the entity object next to its column values and changes. Audit.NET applies field exclusions (`ForEntity<T>(e => e.Ignore(...))`) to the column values and changes only, so the entity copy still carried the excluded fields, the standard ones (`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `ClientSecretHash`) and any an application adds through `BlueprintAuditOptions.EntityFieldConfigurator` alike. Entity objects are now left out (`IncludeEntityObjects(false)`), and an excluded field appears nowhere in the stored entry.

**Behaviour change:** entries in `BlueprintAuditLogs.JsonData` no longer have an `Entity` property under `EntityFrameworkEvent.Entries[]`. `ColumnValues` and `Changes` are unchanged and carry every field that is not excluded.

Consumer migration: code or queries that read `Entries[].Entity` read `Entries[].ColumnValues` instead. Rows written before this release still hold the entity copy; an application that wants the excluded fields gone from existing rows removes `Entity` from them in a data migration.
