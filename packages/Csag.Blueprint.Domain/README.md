# Csag.Blueprint.Domain

## Overview

This package defines the **shared domain model and contracts** for CSAG Blueprint-based applications.

It does **not** contain only interfaces. It also contains the reusable blueprint-owned entity types and base identity/tenant types that the consuming application builds on.

## What these packages assume about your tenancy model

The `Csag.Blueprint.*` packages are deliberately opinionated on exactly **two** points. Everything else about your multi-tenancy design is yours to decide, and the packages will not fight you.

| Assumption | What it means | If you need otherwise |
| --- | --- | --- |
| **Global identity + membership** | One account per real person, shared across tenants. `BlueprintTenantMembership<TUser,TTenant>` is the user↔tenant join, and roles and direct permissions hang off it per tenant. | These packages are the wrong dependency. Tenant-local identity means the membership join has no reason to exist, and no amount of configuration recovers that. |
| **Two tiers** | platform → tenant → users. There is no level above the tenant. | Add your level in your own project and implement `ITenantAuthorizationResolver` to union in its roles — the interface is deliberately opaque, so its signature does not change. |

Everything else — how rows are isolated, how a request declares its tenant, who operates the platform, how much a tenant may customise — is a decision the consuming application makes.

## The rule these packages follow

> **The packages own mechanism. Your application owns vocabulary and policy.**

`TenantRoleService` knows *that* roles map to permissions; it never knows *which* roles exist. The catalogue, the permission names, and the policies that gate endpoints all live in your application, and the packages reach them through injected abstractions such as `IRolePermissionResolver`.

This is the line to hold when extending the packages. If a new type would need to know a specific role or permission name, it belongs in your application, not here.

## Extension points

| Seam | Interface | Swap it to change… |
| --- | --- | --- |
| Tenant resolution | `ITenantResolver` *(in `Csag.Blueprint.Web`)* | how a request declares which tenant it belongs to — session claim, subdomain, path, header |
| Authorization composition | `ITenantAuthorizationResolver`, `IRolePermissionResolver` | how roles map to permissions, and how scopes merge — including adding a hierarchy level |
| Tenant data isolation | `IMustHaveTenant` + `ConfigureBlueprintMultiTenancy` | how rows are isolated. Pass `addTenantForeignKey: false` when tenant-owned data lives in a different database from the tenant table |

## What this package owns

### Contracts

| Type | Purpose |
| --- | --- |
| `IAuditable` | Marks an entity for automatic timestamp management (`CreatedAt`, `UpdatedAt`) by infrastructure interceptors. |
| `IMustHaveTenant` | Marks an entity as tenant-scoped and requires a `TenantId`. Used by query filters and save interceptors. |
| `IUserProfileClaimsSource` | Exposes user profile values that should become claims (such as display name, preferred language). |

### Base identity and tenant types

| Type | Purpose |
| --- | --- |
| `BlueprintUser` | Shared base user type extending ASP.NET Core Identity user infrastructure. |
| `BlueprintRole` | Shared base role type extending ASP.NET Core Identity role infrastructure. |
| `BlueprintTenant` | Shared base tenant type for multi-tenant applications. |

### Shared blueprint-owned entities

| Type | Purpose |
| --- | --- |
| `BlueprintActiveSession` | Tracks active authenticated sessions. |
| `BlueprintAuditLog` | Persists audit events written by Audit.NET integration. |
| `BlueprintResourceAccess` | Shared authorization/resource-access persistence model. |
| `BlueprintServiceAccount` | Represents machine/service credentials for JWT-based authentication. |
| `BlueprintTranslation` | Stores database-backed localization entries. |
| `BlueprintTableViewPreference<TUser>` | Persists per-user table view preferences. |
| `BlueprintTenantMembership<TUser, TTenant>` | Join entity linking users to tenants. |

## Architectural Role

`Csag.Blueprint.Domain` is the stable contract layer for the reusable blueprint model:

- `Csag.Blueprint.Application` builds abstractions on top of these types
- `Csag.Blueprint.Infrastructure` maps and enforces them
- the consuming application derives concrete app types such as `ApplicationUser`, `ApplicationRole`, and `ApplicationTenant`

This package should stay free of application-specific policy and host-specific infrastructure concerns.
