---
"@neolution-ch/csag-blueprint-web": minor
---

Let applications decide which tenant an entity-change audit event is recorded under

`ConfigureBlueprintAuditLogging` records every EF audit event under the ambient `TenantContext.Current`, the actor's active tenant. That is not always the tenant the change belongs to: a platform administrator who changes a global row while a tenant is active records the change under that tenant.

**New:** `BlueprintAuditOptions.EntityChangeTenantResolver`, an `AuditTenantResolver` delegate `(HttpContext? httpContext, Guid? activeTenantId) => Guid?`. It is called once per `SaveChanges`, with the request the change was saved in (null outside one) and the active tenant, and returns the tenant to write to the `TenantId` column, or null for none. Left unset, the active tenant is recorded, as before. HTTP audit events are not affected.

Consumer migration: none.
