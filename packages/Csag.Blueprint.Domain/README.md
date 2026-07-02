# Csag.Blueprint.Domain

## Overview

This package defines the **shared domain model and contracts** for CSAG Blueprint-based applications.

It does **not** contain only interfaces. It also contains the reusable blueprint-owned entity types and base identity/tenant types that the consuming application builds on.

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
