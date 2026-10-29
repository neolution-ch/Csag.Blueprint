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
| Session revocation | `ISessionRevocationParticipant` *(in `Csag.Blueprint.Application`)* | which of the application's own credentials end when sessions are revoked |
| Session eligibility | `ISessionEligibility` *(in `Csag.Blueprint.Application`)* | which accounts may hold a session at all — asked at sign-in and at every sliding renewal |

## What this package owns

### Contracts

| Type | Purpose |
| --- | --- |
| `IAuditable` | Marks an entity for automatic timestamp management (`CreatedAt`, `UpdatedAt`, instants in UTC from the registered `TimeProvider`) by infrastructure interceptors. |
| `IMustHaveTenant` | Marks an entity as tenant-scoped and requires a `TenantId`. Used by query filters and save interceptors. |
| `IUserProfileClaimsSource` | Exposes user profile values that should become claims (such as display name, preferred language). |
| `ISoftDeletable` | Marks an entity as soft deletable via `DeletedAt`. Infrastructure registers a named global query filter that hides deleted rows. |
| `IHasActiveRange` | Marks an entity with an optional `ActiveFrom`/`ActiveUntil` validity window. Deliberately **not** a global filter — the point in time is the caller's choice. |
| `IHasInternalName` | Marks an entity carrying a stable, non-localized key for system identification and reporting (indexed, max 200 chars). |
| `ILocalizedText` | A translation row: a `LanguageCode` and its `Text`. |
| `IHasLocalizedTexts<TLocalizedText>` | Marks an entity owning a collection of `ILocalizedText` rows, one per language. |

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
| `BlueprintServiceAccountSession` | Tracks the server-side sessions behind reference-style service-account JWTs. |
| `BlueprintAuditLog` | Persists audit events written by Audit.NET integration. |
| `BlueprintResourceAccess` | Shared authorization/resource-access persistence model. |
| `BlueprintServiceAccount` | A tenant-owned, non-human account that authenticates with a client id and secret for bearer tokens. Its credentials are managed through `IServiceAccountCredentials` (Csag.Blueprint.Application). |
| `BlueprintTranslation` | Stores database-backed localization entries. |
| `BlueprintTableViewPreference<TUser>` | Persists per-user table view preferences. |
| `BlueprintTenantMembership<TUser, TTenant>` | Join entity linking users to tenants. |

### Time zones

`ZonedTime` converts between instants (`DateTimeOffset`, always returned in UTC) and the local readings of a place
(`DateTime`, the clock reading on a wall there) using `TimeZoneInfo` zones with IANA ids. It lives in Domain so
entities can derive an instant from a local reading themselves.

| Member | Purpose |
| --- | --- |
| `TryToInstant(localReading, zone, out instant)` | The instant a reading denotes. `false` when the reading falls in a spring-forward gap, including past gaps that Linux's `TimeZoneInfo.IsInvalidTime` misses; a reading that happens twice (fall-back overlap) resolves to its earlier occurrence. |
| `ToLocalReading(instant, zone)` | The reading a clock in the zone shows at an instant. |
| `Today(timeProvider, zone)` | Today's date in the zone, not in UTC. |
| `DayRange(date, zone)` | The half-open instant range `[Start, End)` a calendar day spans in the zone: 23 or 25 hours on transition days, starting at the transition when local midnight does not exist. |
| `TryFindIanaTimeZone(id, out zone)` | Finds a zone by its exact IANA id and rejects Windows ids and zoneinfo paths that are no ids (`Europe//Zurich`, `localtime`, `right/UTC`). Needs the host's time zone data (`tzdata` on Linux). Backward links that browsers still send (`Asia/Calcutta`, `Asia/Saigon`, `Europe/Kiev`) resolve only where that data has them: Debian and Ubuntu ship them in `tzdata-legacy`, Alpine's `tzdata` includes them. |
| `IsWellFormedIanaTimeZoneId(id)` | Whether a value has the form of an IANA id (at most 64 characters), known to this host or not: an id from newer time zone data (a browser's) passes, a malformed value does not. |

### Time values

A time that is set as a clock reading but also needed as an instant (to sort, compare or show in another zone)
is stored as both. The time values keep the two in step: the reading is the truth, the instant is derived from it,
and the only way to get a value is a factory that derives it, so an entity cannot hold a pair that disagrees. One
type per kind, all immutable records with value equality in `Csag.Blueprint.Domain.Time`:

| Type | Members | Holds |
| --- | --- | --- |
| `PlaceTime` | `Local`, `At` | A place time: the reading at a place whose zone the caller knows, normally the tenant's (`ITenantTimeZoneProvider`) or that of a place the entity belongs to. The zone is not stored. |
| `ZonedPlaceTime` | `Local`, `TimeZoneId`, `At` | A place time at a place with its own zone, stored with it: a stand at a boat show in Miami for a tenant in Zurich. |
| `ViewerTime` | `Local`, `TimeZoneId`, `At` | A viewer time: the reading an organiser set in their own zone, shown to each viewer in theirs (an online briefing). |

`Local` is a `DateTime` with `DateTimeKind.Unspecified` (the `Kind` of the reading passed in is ignored), `At` a
`DateTimeOffset` in UTC, `TimeZoneId` an IANA id that `ZonedTime.TryFindIanaTimeZone` resolves.

| Member | Purpose |
| --- | --- |
| `TryCreate(local, zone, out value)` / `Create(local, zone)` | `PlaceTime` from a reading and the zone of its place. `false` (or `ArgumentException`) when the reading falls in a spring-forward gap; a reading in a fall-back overlap denotes its earlier occurrence, as in `ZonedTime.TryToInstant`. |
| `TryCreate(local, timeZoneId, out value)` / `Create(local, timeZoneId)` | `ZonedPlaceTime` and `ViewerTime`; also `false` (or `ArgumentException`) for an id that is no IANA id this host resolves. |
| `Rederive(zone)` (`PlaceTime`), `Rederive()` (zoned types) | Keeps the reading and derives the instant again: after a time zone data update, or for a `PlaceTime` whose place now has another zone. Returns `(Value, Shifted)`. |
| `WithTimeZone(timeZoneId)` (zoned types) | Keeps the reading in another zone and derives its instant there. Returns `(Value, Shifted)`. |

When the new rules put a reading into a spring-forward gap, `Rederive` and `WithTimeZone` move it forward by the length
of the gap (02:30 in a gap from 02:00 to 03:00 becomes 03:30, 02:15 in Lord Howe's half-hour gap 02:45) and return
`Shifted: true`, so the job that calls them can tell whoever relies on the reading.

DTOs stay flat: a request carries the reading (and a `TimeZoneId` for the zoned types) and the endpoint builds the value
with the factory; a response or table view row carries the members it needs (`StartLocal`, `StartAt`). EF Core maps the
types as complex types (they carry `[ComplexType]`); `Csag.Blueprint.Infrastructure` names their columns.

## Architectural Role

`Csag.Blueprint.Domain` is the stable contract layer for the reusable blueprint model:

- `Csag.Blueprint.Application` builds abstractions on top of these types
- `Csag.Blueprint.Infrastructure` maps and enforces them
- the consuming application derives concrete app types such as `ApplicationUser`, `ApplicationRole`, and `ApplicationTenant`

This package should stay free of application-specific policy and host-specific infrastructure concerns.
