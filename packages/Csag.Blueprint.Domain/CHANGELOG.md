# @neolution-ch/csag-blueprint-domain

## 0.11.0

No changes in this release.

## 0.10.0

No changes in this release.

## 0.9.0

### Minor Changes

- [#66](https://github.com/neolution-ch/Csag.Blueprint/pull/66) [`4b342d1`](https://github.com/neolution-ch/Csag.Blueprint/commit/4b342d1002a6f896440e83bb1bbfd4eb25562f17) Thanks [@neotrow](https://github.com/neotrow)! - Model dates, local readings and instants as distinct kinds, with a tenant time zone, zone-aware table views and real date cells in exports
  
  A `DateTime` in a contract now always means a local reading (the clock reading at a place, no offset) and a `DateTimeOffset` an instant. The packages give applications the pieces to keep the two apart: a tenant zone seam, conversions that handle daylight saving time, validation rules, and table views that filter and export each kind correctly.
  
  **New:** `TimeZones` (Csag.Blueprint.Domain, namespace `Csag.Blueprint.Domain.Time`), a static helper over `TimeZoneInfo` with IANA ids: `TryToInstant(localReading, zone, out instant)` (false in a spring-forward gap, including past gaps that Linux's `TimeZoneInfo.IsInvalidTime` misses in zones that later dropped daylight saving time; a fall-back overlap resolves to the earlier occurrence; instants are returned in UTC), `ToLocalReading(instant, zone)`, `Today(timeProvider, zone)`, `DayRange(date, zone)` (half-open `[Start, End)`, 23 or 25 hours on transition days, starting at the transition when local midnight does not exist), `TryFindIanaTimeZone(id, out zone)` (exact, case-sensitive IANA ids only; Windows ids such as `W. Europe Standard Time` and paths such as `Europe//Zurich`, `localtime` or `right/UTC`, which `TimeZoneInfo` also resolves, are rejected; backward links such as `UTC` are accepted where the host's data has them; Debian and Ubuntu ship `Asia/Calcutta`, `Asia/Saigon`, `Europe/Kiev` and other links browsers still send in `tzdata-legacy`), and `IsWellFormedIanaTimeZoneId(id)`, which tells an id of the IANA form (at most 64 characters) that this host's time zone data may not know from a value that is no id at all.
  
  **New:** `PlaceTime` and `ZonedTime` (Csag.Blueprint.Domain, namespace `Csag.Blueprint.Domain.Time`), immutable records that hold a local reading together with the instant derived from it, so an entity cannot hold a reading and an instant that disagree. They differ in where the zone comes from: `PlaceTime` (`Local`, `At`) is a place time whose zone the caller knows (normally the tenant's, or that of a place the entity belongs to) and does not store; `ZonedTime` (`Local`, `TimeZoneId`, `At`) stores its zone with the reading. A `ZonedTime` holds either a place time at a place with its own zone (a boat-show stand in Miami, `America/New_York`, for a Zurich tenant), shown as the clock time there and labelled with its zone, or a viewer time an organiser sets in their own zone (an online briefing at 10:00 `Europe/Zurich`), shown to each viewer in theirs; the entity property and its DTO decide which display rule applies, not the type. `Local` is a `DateTime` with `DateTimeKind.Unspecified` (the `Kind` of the reading passed in is ignored), `At` a `DateTimeOffset` in UTC. The only way to get a value is `PlaceTime.TryCreate(local, zone, out value)` / `Create(local, zone)` or `ZonedTime.TryCreate(local, timeZoneId, out value)` / `Create(local, timeZoneId)`, which derive `At` with `TimeZones.TryToInstant`: a reading in a spring-forward gap fails (`false`, or an `ArgumentException` naming the reading), one in a fall-back overlap denotes its earlier occurrence, and `ZonedTime` also refuses an id `TimeZones.TryFindIanaTimeZone` rejects. `PlaceTime.Rederive(zone)` and `ZonedTime.Rederive()` keep the reading and derive the instant again, for the job an application runs after a time zone data update or a change of the tenant's zone; `ZonedTime.WithTimeZone(timeZoneId)` keeps the reading in another zone. Both return `(Value, Shifted)`: a reading the new rules put into a spring-forward gap moves forward by the length of the gap (02:30 becomes 03:30), and `Shifted` is `true` so the caller can log it. DTOs stay flat, with no converters or schema processors for these types: a request carries the reading (and a `TimeZoneId` for a `ZonedTime`), and the endpoint builds the value with the factory.
  
  **New:** EF Core maps the time values as complex types without any configuration (they carry `[ComplexType]`), and the blueprint conventions name their columns after the property: `{Property}Local` (`datetime2`), `{Property}At` (`datetimeoffset`) and `{Property}TimeZoneId` (`nvarchar(100)`), so `PlaceTime Start` is stored in `StartLocal` and `StartAt`. An optional property (`PlaceTime?`) gets nullable columns. A column the application names explicitly keeps its name. `BlueprintDbContext` applies the convention at model finalization; a context that does not derive from it calls `TimeValueModelBuilderExtensions.ConfigureTimeValueColumns()` (Csag.Blueprint.Infrastructure) last in `OnModelCreating`. Checked on SQL Server: round trips of both types, required and optional; `Where`, `OrderBy`, `== null` and projections into flat DTOs through the members, all in SQL; `ExecuteUpdateAsync` setting a value or clearing an optional one; table view columns over members (`Column(dto => dto.StartLocal, e => e.Start.Local)`, `Column(dto => dto.StartAt, e => e.Start.At)`, and a `ZonedTime` reading with `WithTimeZoneFrom`) filtering, sorting and exporting. EF Core 10 cannot index a member of a complex type (`HasIndex(e => e.Start.At)` throws `ArgumentException: ... is not a valid member access expression`): create the index on the column in the migration (`migrationBuilder.CreateIndex(name: "IX_Reservations_StartAt", table: "Reservations", column: "StartAt")`). Audit.NET records a null optional value's columns as `0001-01-01T00:00:00` and a null `TimeZoneId` rather than as nulls. EF Core 10 creates no columns for a complex property declared on the abstract root of a table-per-concrete-type hierarchy, so declare time values on the concrete types there.
  
  **New:** `ITenantTimeZoneProvider` (Csag.Blueprint.Application) supplies the current tenant's zone. Applications implement it; `AddBlueprintTenancyRuntime`, `AddBlueprintTableViewCore` and `AddBlueprintServices` register `UtcTenantTimeZoneProvider` (Csag.Blueprint.Infrastructure) with `TryAdd`, so every tenant is in UTC until the application registers its own.
  
  **New:** `TimeZoneRuleBuilderExtensions` (Csag.Blueprint.Web.Validation): `MustBeIanaTimeZone()` for zones that are stored (a tenant's, a venue's), which must resolve; `MustBeWellFormedIanaTimeZone()` for a zone a request names for itself, which only needs the IANA form; `MustNotBeAfterToday(timeProvider, timeZone)` for `DateOnly`/`DateOnly?`, and `MustExistInTimeZone(timeZone)` for `DateTime`/`DateTime?`. The zone-dependent rules take a `Func<T, CancellationToken, ValueTask<TimeZoneInfo>>` that runs on every validation, because FastEndpoints validators are singletons: pass `(_, ct) => this.Resolve<ITenantTimeZoneProvider>().GetTimeZoneAsync(ct)`. They are asynchronous rules.
  
  **New:** `TimeZoneDataHealthCheck` and `AddBlueprintTimeZoneDataCheck()` (Csag.Blueprint.Web.HealthChecks): Unhealthy when the host cannot resolve `Europe/Zurich`, i.e. the image has no time zone data; Degraded when it cannot resolve `Asia/Calcutta`, the legacy name Chromium sends for India, i.e. Debian or Ubuntu without `tzdata-legacy` (those viewers' table views fall back to the tenant zone); registered as `time_zone_data_check`, tagged `ready`. Both probe ids can be overridden.
  
  **New:** `TableViewDataTypes` (Csag.Blueprint.Application) names the `TableViewColumnMetadata.DataType` values.
  
  **New:** `TableViewColumnDefinition.WithTimeZoneFrom(Expression<Func<TDto, string?>> timeZoneSelector)` declares that a local reading column's readings are in the zone its row names instead of the tenant's, for places that store their own IANA `TimeZoneId` (a site the tenant runs, a one-off venue). The selector must name a property of the DTO itself (`dto => dto.TimeZoneId`; `dto => dto.Venue.TimeZoneId` is refused with an `ArgumentException`), and that property must be a column; `TableViewColumnMetadata.TimeZoneColumn` names it, so clients label each reading with its row's zone. Filters on the column are unchanged (local readings compare as they read), and its export header names no zone. Only local reading columns accept it.
  
  ```csharp
  Column(dto => dto.OpensLocal)
      .Filterable(TableViewFilterOperator.DateRange)
      .WithTimeZoneFrom(dto => dto.TimeZoneId);
  Column(dto => dto.TimeZoneId);
  ```
  
  **New:** `TableViewDataRequest.TimeZone` and `TableViewExportRequest.TimeZone`, an optional IANA id (normally the viewer's browser zone). `TableViewDataRequestValidator` and `TableViewExportRequestValidator` reject a value that does not have the IANA form with a 400 validation error. Without it, the tenant zone applies, and UTC when there is none. Browsers can know zones the server's time zone data does not yet (`America/Coyhaique`), so a well-formed id the server cannot resolve is no error: the request falls back to the tenant zone, a warning is logged (once per id and process, then at debug level), and an export's headers name the zone actually used. An id longer than 64 characters is not well-formed. `TableViewRequestTimeZone.TryFindRequested(id, logger, out zone)` (Csag.Blueprint.Web) applies that rule for endpoints that handle these requests themselves. `TableViewQueryRequest.TimeZone` carries the resolved zone to the executor.
  
  **New:** `LocalDateTimeFormat` (Csag.Blueprint.Application.Json): `Format`, `TryParse` and `GetInvalidMessage` for the text form of a local reading, shared by the JSON converter, query/route/form binding and table view filters.
  
  **New:** `InstantFormat` (Csag.Blueprint.Application.Json): `TryParse` for the text form of an instant, ISO 8601 with a UTC offset or `Z` as System.Text.Json's own parser reads it, shared by `InstantJsonConverter` and query/route/form/header binding.
  
  **Breaking:** `TableViewColumnMetadata.DataType` reports `localDateTime` for a `DateTime` column and `instant` for a `DateTimeOffset` column; only `DateOnly` columns still report `date`.
  
  **Breaking:** `BlueprintJsonOptions` registers `LocalDateTimeJsonConverter` (Csag.Blueprint.Application) for `DateTime` and `DateTime?`, on the wire and in JSON the packages persist. It writes `yyyy-MM-ddTHH:mm:ss` (no fraction, no offset) and reads `yyyy-MM-ddTHH:mm:ss` or `yyyy-MM-ddTHH:mm` with `DateTimeKind.Unspecified`. A value with a UTC offset or `Z`, a fraction, or any other format is refused with a 400 naming the field ("…without a UTC offset or 'Z'…"). Writing a `DateTime` whose `Kind` is `Utc` or `Local`, as a value or a dictionary key, throws an `InvalidOperationException` naming the kind: such a value holds an instant or the server's clock (`DateTime.UtcNow`, `.UtcDateTime`, `.LocalDateTime`, `ToLocalTime()`), which a reading without an offset would misrepresent. Readings EF Core reads from `datetime2` columns, and those `TimeZones` and the time values return, are `Unspecified`. None of the packages' own contracts used `DateTime`.
  
  **Breaking:** `BlueprintJsonOptions` registers `InstantJsonConverter` (Csag.Blueprint.Application) for `DateTimeOffset` and `DateTimeOffset?`, dictionary keys included, on the wire and in JSON the packages persist. It reads with System.Text.Json's own ISO 8601 parser, so every value with a UTC offset or `Z` accepted before still is, and keeps its offset. A value without one (`2026-07-01T10:00:00`), which System.Text.Json read in the server's zone, a date on its own (`2026-07-01`) and a non-string token are refused with a 400 naming the field ("…with a UTC offset or 'Z'…"). Writing is unchanged.
  
  **Breaking:** `UseFastEndpointsWithConventions` binds `DateTime` and `DateTime?` from query, route, form and header values with the same formats as a JSON body, and `DateTimeOffset` and `DateTimeOffset?` with the same rule as one (`InstantFormat`). FastEndpoints' own parsers accepted offsets or `Z` for a `DateTime` (converting to the server's zone, `Kind=Local`), fractions, culture formats such as `25.10.2026 10:00` and bare dates, and read a `DateTimeOffset` without an offset in the server's zone; those now answer 400 with FastEndpoints' binding failure message. An empty value is refused for a `DateTime` or a `DateTimeOffset`. An omitted nullable value binds null, and an empty `DateTime?` or `DateTimeOffset?` query or form value binds `default`: FastEndpoints' `UseDefaultValuesForNullableProps` (on by default) assigns empty nullable values without consulting the parser's verdict.
  
  **Breaking:** The `IHasActiveRange` query extensions that read the clock take it as a parameter (`EntityFilteringExtensions`, Csag.Blueprint.Infrastructure): `WhereActiveNow(timeProvider)`, `WhereInactiveNow(timeProvider)` and `WhereActiveAndNotDeleted(timeProvider)` compare against `timeProvider.GetUtcNow()`, and `WhereActiveToday(timeProvider, timeZone)` compares against the start of today in `timeZone` (`TimeZones.Today`, then the start of `TimeZones.DayRange`) instead of midnight UTC, so a Zurich tenant's day starts at 22:00 or 23:00 UTC the evening before. `WhereActiveAt` and `WhereActiveInRange` are unchanged.
  
  **Breaking:** `DateRange` filters take calendar dates only (`yyyy-MM-dd,yyyy-MM-dd`, either side optional) and mean whole days per kind: `date` matches `[from, to]` inclusive; `localDateTime` matches `>= from 00:00` and `< (to + 1 day) 00:00` on the reading; `instant` matches `>= start of from` and `< start of (to + 1 day)` in the effective zone. No range matches a row without a value, not even `,9999-12-31`, which has no bound because the day after it cannot be represented. A one-day filter on an instant or local reading column now covers the whole day instead of only 00:00:00, and full timestamps as range bounds are no longer accepted (the filter is ignored, like any malformed value). Temporal values are parsed as ISO 8601 only, never in the server's culture or zone: an `Equals` value on a `DateOnly` column is `yyyy-MM-dd`, on a `DateTime` column `yyyy-MM-ddTHH:mm[:ss]` without an offset, and on a `DateTimeOffset` column either carries an offset or `Z` or is read as a local reading in the effective zone. A local reading for an instant column that does not exist in that zone (a spring-forward gap) is well-formed but names no instant, so an `Equals` or `In` entry with it matches no row.
  
  **Breaking:** `ITableViewDefinition.GetFilterExpression` takes a `TimeZoneInfo timeZone` third parameter. `TableViewDefinition` keeps the two-argument overload, which evaluates instant filters in UTC, so definition tests keep compiling.
  
  **Breaking:** `ITableViewExecutor` gains a member, `ExecuteWithoutPaginationAsync(query, definition, filters, sortColumns, TimeZoneInfo? timeZone, cancellationToken)`. Migration: an application class that implements the interface (a decorator, a test fake) adds the overload, typically forwarding to its inner executor or to the existing overload.
  
  **Breaking:** `TableViewExecutor` takes an `ITenantTimeZoneProvider` in its constructor. It is resolved from DI as before; code that constructs it directly passes one (`new UtcTenantTimeZoneProvider()` in tests).
  
  **Behaviour change:** `TableViewExcelExporter` writes temporal values as real date cells: a `DateOnly` as a date, a local reading as it reads, and an instant converted to the effective zone with its time (previously its UTC date with the time dropped). Number formats follow the request culture's short date and time patterns (instants include seconds) and its calendar instead of the fixed `dd.MM.yyyy`: a `th-TH` export shows Buddhist years and an `fa-IR` export Persian dates, as the UI does (a culture's default calendar comes from the host's ICU data; Excel shows Taiwan-calendar years as Gregorian ones). The headers of columns with a time name their zone as an IANA id after the column header: an instant column the effective zone (`Created at (Europe/London)`, the tenant's or UTC when the requested zone was unknown), a local reading column the tenant's zone (`Start (Europe/Zurich)`); date columns are unchanged, and so is a local reading column whose zone comes from its row (see `WithTimeZoneFrom`), since its rows can be in different zones that the zone column carries. `Export` takes optional `timeZone` (the zone instants are shown in and named by; UTC when null), `placeTimeZone` (named by local reading columns; none when null) and `culture` (current culture when null) parameters; `TableViewExportEndpointBase` passes the effective zone, the tenant zone and `CultureInfo.CurrentCulture`.
  
  **Behaviour change:** The OpenAPI document describes a `DateTime` as `type: string, format: date-time-local, pattern: ^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$` (instead of `format: date-time`), a `TimeOnly` as `type: string, format: time-local, pattern: ^\d{2}:\d{2}:\d{2}(\.\d{1,7})?$` (instead of `format: time`, which is RFC 3339 `full-time` and requires an offset), and a `TimeSpan` as a string with the pattern of its constant format `[-][d.]hh:mm:ss[.fffffff]`, without `format: duration` (`TemporalSchemaProcessor`, registered by `AddSwaggerDocumentation`). `date-time-local` and `time-local` are the names the OpenAPI Format Registry gives RFC 3339 date-times and times without the time zone component. The `TimeOnly` pattern is what System.Text.Json writes (`HH:mm:ss`, a seven-digit fraction when there is one); it also reads `HH:mm` and one-digit hours, which the pattern leaves out.
  
  **Behaviour change:** Every `DateTimeOffset` and `DateTimeOffset?` property in the model, of an entity, an owned type or a complex type (a time value's `At` included), gets a value converter that writes `value.ToUniversalTime()` and reads the stored value unchanged, so an instant saved as `10:00+02:00` is stored and read back as `08:00+00:00`, the same instant. Npgsql writes a `DateTimeOffset` to a `timestamptz` column only at offset zero and throws for any other; on SQL Server every stored instant is now `+00:00`, the way instants travel in the API. Query parameters compared with such a property are converted the same way, so filters written with another offset match the same rows. A property with a value converter of its own keeps it. `BlueprintDbContext` applies the converter at model finalization; a context that does not derive from it gets it from `ConfigureTimeValueColumns()`. Rows stored earlier with another offset read back as stored.
  
  **Fix:** The `IAuditable.CreatedAt` / `UpdatedAt` docs name the registered `TimeProvider` as the clock the interceptor stamps with.
  
  No migration is needed for existing entities: the time value conventions only apply to properties of the new types, and the UTC converter changes no column type, so neither changes the migration model snapshot until an application adds a time value.
  
  Consumer migration (`csag-blueprint-web`):
  
  - Frontend `components/table-view/cells.tsx` switches on `dataType`: add `localDateTime` (format the reading as it reads, labelled with the place zone when the viewer's differs: the zone in the row's `timeZoneColumn` field when the column names one, else the tenant zone) and `instant` (format in the viewer's zone); until then both fall through to raw text. Send the viewer's zone (`Intl.DateTimeFormat().resolvedOptions().timeZone`) as `timeZone` on table view data and export requests.
  - Every client that sends a `DateTime` must send `yyyy-MM-ddTHH:mm[:ss]` without an offset or `Z`. Today's form-kit `DateTimeInputField` appends the browser offset, so the Example `TestEndpoint` (`DateTimeValue`, `OptionalDateTime`) answers 400 until the form sends local readings. Its `new DateTime(..., DateTimeKind.Utc)` values are OpenAPI examples, and an example, a response or a serialized request with such a value throws, so build readings with `DateTimeKind.Unspecified`.
  - Every client that sends a `DateTimeOffset` must include a UTC offset or `Z` (`Date.prototype.toISOString()` does); a value without one, or a date on its own, answers 400. Check raw JSON in tests and seeds as well.
  - A `DateTime` used as an instant in a DTO loses its meaning on the wire, and writing one with `Kind` `Utc` or `Local` throws: change it to `DateTimeOffset`. Test code that builds request DTOs with `DateTimeKind.Utc` and posts them with `BlueprintJsonOptions` throws too.
  - Pass the injected `TimeProvider` to `WhereActiveNow`, `WhereInactiveNow` and `WhereActiveAndNotDeleted`, and the tenant's zone (`ITenantTimeZoneProvider`) as well to `WhereActiveToday`.
  - Regenerate the OpenAPI spec and the Orval client: `DateTime` properties change format to `date-time-local`, `TimeOnly` properties to `time-local`, and `TimeSpan` properties lose `format: duration`; the generated TypeScript types stay `string`.
  - Replace a hand-kept pair of reading and instant (`DateTime StartLocal` + `DateTimeOffset StartsAt`) with a time value (`PlaceTime Start`), built with its factory wherever the reading changes. To keep existing columns, name them on the complex property (`ComplexProperty(r => r.Start, s => s.Property(t => t.At).HasColumnName("StartsAt"))`); an index on such a column moves into the migration (`migrationBuilder.CreateIndex`).
  - Implement `ITenantTimeZoneProvider` over the tenant's `TimeZoneId` and register it; add `AddBlueprintTimeZoneDataCheck()` to the health checks.
  - Use `MustNotBeAfterToday` instead of comparisons with `DateOnly.FromDateTime(DateTime.UtcNow)`, and `MustExistInTimeZone` on local readings.

## 0.8.0

### Patch Changes

- [#76](https://github.com/neolution-ch/Csag.Blueprint/pull/76) [`ba77ed4`](https://github.com/neolution-ch/Csag.Blueprint/commit/ba77ed4f0708084b43417fb5c9ab1ee0a5d0fe48) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 9 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | coverlet.collector | 10.0.1 | 10.1.0 | 🟡 minor |
  | FluentValidation | 12.1.1 | 12.2.0 | 🟡 minor |
  | Microsoft.Data.SqlClient | 7.0.3 | 7.1.1 | 🟡 minor |
  | Microsoft.Extensions.TimeProvider.Testing | 10.10.0 | 10.11.0 | 🟡 minor |
  | Microsoft.NET.Test.Sdk | 18.10.1 | 18.11.0 | 🟡 minor |
  | Testcontainers.MsSql | 4.15.0 | 4.16.0 | 🟡 minor |
  | WireMock.Net.Testcontainers | 2.15.0 | 2.19.0 | 🟡 minor |
  | xunit.runner.visualstudio | 4.0.0 | 4.0.1 | 🟢 patch |
  | xunit.v3.mtp-off | 4.0.1 | 4.0.2 | 🟢 patch |

- [#70](https://github.com/neolution-ch/Csag.Blueprint/pull/70) [`d6ba798`](https://github.com/neolution-ch/Csag.Blueprint/commit/d6ba798344ed41169f0af77f25d1dfd6fc890843) Thanks [@neotrow](https://github.com/neotrow)! - Raise vulnerable dependencies to patched versions
  
  The ASP.NET Core and EF Core packages move to the 10.0.13 servicing release. It brings
  `System.Security.Cryptography.Xml` 10.0.13 (GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj,
  GHSA-r955-8q5j-3h83) and `System.Security.Cryptography.Pkcs` 10.0.13 (GHSA-846f-f764-76mx,
  GHSA-2fmv-rv2c-59x7).
  
  `Csag.Blueprint.Infrastructure`, `Csag.Blueprint.Web` and `Csag.Blueprint.Testing` now also depend
  on `System.Security.Cryptography.Pkcs` 10.0.13. The package that pulls it in,
  `Microsoft.Data.SqlClient`, still asks for a vulnerable version. Upgrading the blueprint packages is
  enough to get the patched version, without a pin of your own.
  
  **When upgrading:**
  
  - Your own `Microsoft.AspNetCore.*` and `Microsoft.EntityFrameworkCore.*` references must be 10.0.13
    or later, or restore fails with a package downgrade error (NU1605, or NU1109 under central package
    management).
  - Run your applications on the .NET 10.0.13 runtime or later. Microsoft fixes these advisories in
    the runtime as well as in the packages, and the Xml advisory also covers `System.Private.Xml`,
    which only ships with the runtime. Update the installed runtime or the container base image, and
    rebuild and redeploy self-contained applications.

- [#75](https://github.com/neolution-ch/Csag.Blueprint/pull/75) [`5872a19`](https://github.com/neolution-ch/Csag.Blueprint/commit/5872a19d6296c06a325f24d2be0fd64d5a685a44) Thanks [@neoscie](https://github.com/neoscie)! - Update `Neolution.Extensions.Caching.Distributed` to 3.0.0
  
  `Csag.Blueprint.Infrastructure`, `Csag.Blueprint.Web` and `Csag.Blueprint.Testing` now depend on
  `Neolution.Extensions.Caching.Distributed` 3.0.0. This version depends on
  `Foundatio.MessagePack` 13.0.5. `Foundatio.MessagePack` 13.0.5 requires `MessagePack` 3.1.12, which
  fixes GHSA-qhrr-8q5h-9q3h. Thus, the blueprint packages do not pin `MessagePack` now.
  
  **When you upgrade:**
  
  - If your central package versions set `Neolution.Extensions.Caching.Distributed`, set it to
    3.0.0 or later. If you do not, restore fails with a package downgrade error (NU1605, or
    NU1109 with central package management).

## 0.7.0

### Minor Changes

- [#44](https://github.com/neolution-ch/Csag.Blueprint/pull/44) [`dbb4310`](https://github.com/neolution-ch/Csag.Blueprint/commit/dbb4310799f3cdf954e2f9247961670cc32a1652) Thanks [@neotob](https://github.com/neotob)! - Add entity contracts for soft deletion, active ranges, internal names and localized texts, with named global query filters
  
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

## 0.6.0

No changes in this release.

## 0.5.0

No changes in this release.

## 0.4.0

### Patch Changes

- [#48](https://github.com/neolution-ch/Csag.Blueprint/pull/48) [`1e4037d`](https://github.com/neolution-ch/Csag.Blueprint/commit/1e4037d1733ed52f70b7851f136c561ee5e48674) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 18 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | Audit.EntityFramework.Core | 32.3.1 | 33.0.0 | 🔴 major |
  | Audit.NET | 32.3.1 | 33.0.0 | 🔴 major |
  | Audit.NET.SqlServer | 32.3.1 | 33.0.0 | 🔴 major |
  | Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Data.SqlClient | 7.0.2 | 7.0.3 | 🟢 patch |
  | Microsoft.EntityFrameworkCore | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.EntityFrameworkCore.InMemory | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.EntityFrameworkCore.SqlServer | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Extensions.Caching.SqlServer | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.11 | 10.0.12 | 🟢 patch |
  | Microsoft.Extensions.TimeProvider.Testing | 10.9.0 | 10.10.0 | 🟡 minor |
  | Microsoft.NET.Test.Sdk | 18.7.0 | 18.10.1 | 🟡 minor |
  | NLog.Web.AspNetCore | 6.2.0 | 6.2.1 | 🟢 patch |
  | Testcontainers.MsSql | 4.14.0 | 4.15.0 | 🟡 minor |
  | xunit.runner.visualstudio | 3.1.5 | 4.0.0 | 🔴 major |
  | xunit.v3.mtp-off | 4.0.0 | 4.0.1 | 🟢 patch |

## 0.3.0

### Minor Changes

- [#37](https://github.com/neolution-ch/Csag.Blueprint/pull/37) [`05103b1`](https://github.com/neolution-ch/Csag.Blueprint/commit/05103b1f33f6c17ef5e8745d11fef3c2acdf23a9) Thanks [@neotrow](https://github.com/neotrow)! - Add reference-style, revocable service-account sessions
  
  `IServiceAccountSessionManager` (registered by `AddBlueprintSessionInfrastructure`) tracks a server-side `BlueprintServiceAccountSession` per issued service-account JWT and resolves the account's current tenant, roles, and permissions on every request, so revocation, secret rotation, and deactivation take effect immediately. Adds the `IdentityClaimTypes.ServiceAccountSessionId` (`sid`) claim type, the `CacheId.ServiceAccountSession` cache namespace, and an audit exclusion for the new tracking table.
  
  This changes the EF model: `BlueprintDbContext` now maps the `BlueprintServiceAccountSessions` table. Consuming applications need their own EF Core migration for the new table.
  
  `ValidateSessionAsync` takes a `string?`. A token carrying no `sid` claim yields `null` from `FindFirstValue`, and the method is documented to report a missing key as "no session" rather than raise, so the signature no longer forces callers to suppress nullable analysis or add a guard the method already performs.
  
  `RevokeSessionAsync` removes the marker under the key the tracking row stores, not only the key it was called with. The cache compares the key it is handed, while the row predicate becomes SQL string equality, which folds case under a case-insensitive column collation and ignores trailing spaces under every SQL Server collation. A spelling that matched the row but not the marker previously deleted the row and left the marker authorizing the revoked session on the fast path, unreachable by any later revoke because revocation enumerates rows.
  
  A cache that refuses to compose a key no longer surfaces as an exception from the read paths. The byte budget the manager enforces is measured against the cache's default key options, so an application configuring an environment prefix or a schema version has a lower effective ceiling, and the abstraction exposes no way to read either setting back. Such a key can address no stored entry — the same composition refused the write — so `ValidateSessionAsync` reports "no session" and `RevokeSessionAsync` continues to its row delete, instead of turning a malformed `sid` into an error in the authentication pipeline.
  
  Issuance treats everything after the tracking row is committed as an uncancellable obligation. The opportunistic reap of expired rows no longer decides whether a session is issued; a failed marker write removes the marker before the row, because a write that throws may still have been accepted by the backend; and the confirmation that the row survived issuance runs to completion rather than abandoning a live marker on a cancelled request.

## 0.2.0

### Patch Changes

- [#34](https://github.com/neolution-ch/Csag.Blueprint/pull/34) [`338208d`](https://github.com/neolution-ch/Csag.Blueprint/commit/338208d4cb833eadfb2aae7fd0bb48447ad17241) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 3 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | FastEndpoints | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Swagger | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Testing | 8.2.0 | 8.3.0 | 🟡 minor |

## 0.1.3

### Patch Changes

- [#23](https://github.com/neolution-ch/Csag.Blueprint/pull/23) [`e44bd77`](https://github.com/neolution-ch/Csag.Blueprint/commit/e44bd77c56acd2e391c77ee09540ef292ae7260b) Thanks [@LarsMarty](https://github.com/LarsMarty)! - Add a TenantId column to audit log entries
  
  Consuming applications need their own EF Core migration to add the nullable TenantId column.

- [#22](https://github.com/neolution-ch/Csag.Blueprint/pull/22) [`4cdb315`](https://github.com/neolution-ch/Csag.Blueprint/commit/4cdb315d90d7085db77400b7defac4ea6c08adf4) Thanks [@neotrow](https://github.com/neotrow)! - Update all dependencies to their latest versions and regenerate the lock files
  so transitive dependencies are refreshed as well.
  
  **Consuming these packages now requires the .NET SDK 10.0.4xx feature band
  (10.0.400 or newer).** `Csag.Blueprint.SourceGenerators` is built against Roslyn
  5.9.0, which ships only in that band, and the compiler refuses to load an
  analyzer referencing a Roslyn newer than the one running the build. On an older
  SDK the translation-key generator is skipped with a `CS9057` warning and the
  generated types go missing, which surfaces as `CS0103`/`CS0246` errors rather
  than as an obvious SDK problem. IDEs run their own Roslyn for design-time
  generation, so Visual Studio and Rider need to be new enough too. See the SDK
  requirement table in the README.
  
  `Microsoft.ApplicationInsights` stays on the 2.x branch.
  
  `Csag.Blueprint.Web` no longer pins `Microsoft.Data.SqlClient` below the
  centrally managed version and now resolves 7.0.2 in line with the other
  packages, which also brings its `Microsoft.Data.SqlClient.Extensions.Abstractions`
  and `.Internal.Logging` dependencies up from 1.0.0 to 7.0.2.

## 0.1.2

### Patch Changes

- [#20](https://github.com/neolution-ch/Csag.Blueprint/pull/20) [`453255b`](https://github.com/neolution-ch/Csag.Blueprint/commit/453255bf2f5aa0c1cf0aebf0750a19b9dce4f5ba) Thanks [@neoscie](https://github.com/neoscie)! - Add the email address and the display name of the user to audit events

  Audit events now contain `UserEmail` and `UserDisplayName` with `UserId`. The package reads these
  values from the claims on the request when it writes the event. One helper reads the claims for the
  Entity Framework path and for the HTTP path. Therefore an application can show the name of the user
  without a query on the user table.

  A service account has no email address. Therefore its email value is null, and its display name is
  the account name from the token.

  The package writes the two new values to the `JsonData` column, at `$.UserEmail` and
  `$.UserDisplayName`. This release does not change the schema. An application does not need a
  migration.

## 0.1.1

### Patch Changes

- [#15](https://github.com/neolution-ch/Csag.Blueprint/pull/15) [`53adf9c`](https://github.com/neolution-ch/Csag.Blueprint/commit/53adf9c6f519a6306c25bda3b68b6848a7db9496) Thanks [@neotrow](https://github.com/neotrow)! - Update to the latest blueprint packages from csag-blueprint-web

## 0.1.0

### Minor Changes

- [`428bb6f`](https://github.com/neolution-ch/Csag.Blueprint/commit/428bb6fc58a912f5f0d53e0a64799e12c90a8ad4) Thanks [@neotrow](https://github.com/neotrow)! - initial release
