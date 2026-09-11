---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Add reference-style, revocable service-account sessions

`IServiceAccountSessionManager` (registered by `AddBlueprintSessionInfrastructure`) tracks a server-side `BlueprintServiceAccountSession` per issued service-account JWT and resolves the account's current tenant, roles, and permissions on every request, so revocation, secret rotation, and deactivation take effect immediately. Adds the `IdentityClaimTypes.ServiceAccountSessionId` (`sid`) claim type, the `CacheId.ServiceAccountSession` cache namespace, and an audit exclusion for the new tracking table.

This changes the EF model: `BlueprintDbContext` now maps the `BlueprintServiceAccountSessions` table. Consuming applications need their own EF Core migration for the new table.
