---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Model dates, local readings and instants as distinct kinds, with a tenant time zone, zone-aware table views and real date cells in exports

A `DateTime` in a contract now always means a local reading (the clock reading at a place, no offset) and a `DateTimeOffset` an instant. The packages give applications the pieces to keep the two apart: a tenant zone seam, conversions that handle daylight saving time, validation rules, and table views that filter and export each kind correctly.

**New:** `ZonedTime` (Csag.Blueprint.Domain, namespace `Csag.Blueprint.Domain.Time`), a static helper over `TimeZoneInfo` with IANA ids: `TryToInstant(localReading, zone, out instant)` (false in a spring-forward gap; a fall-back overlap resolves to the earlier occurrence; instants are returned in UTC), `ToLocalReading(instant, zone)`, `Today(timeProvider, zone)`, `DayRange(date, zone)` (half-open `[Start, End)`, 23 or 25 hours on transition days, starting at the transition when local midnight does not exist), and `TryFindIanaTimeZone(id, out zone)` (exact, case-sensitive IANA ids only; Windows ids such as `W. Europe Standard Time`, which `TimeZoneInfo` also resolves, are rejected; backward links such as `UTC` are accepted).

**New:** `ITenantTimeZoneProvider` (Csag.Blueprint.Application) supplies the current tenant's zone. Applications implement it; `AddBlueprintTenancyRuntime`, `AddBlueprintTableViewCore` and `AddBlueprintServices` register `UtcTenantTimeZoneProvider` (Csag.Blueprint.Infrastructure) with `TryAdd`, so every tenant is in UTC until the application registers its own.

**New:** `TimeZoneRuleBuilderExtensions` (Csag.Blueprint.Web.Validation): `MustBeIanaTimeZone()`, `MustNotBeAfterToday(timeProvider, timeZone)` for `DateOnly`/`DateOnly?`, and `MustExistInTimeZone(timeZone)` for `DateTime`/`DateTime?`. The zone-dependent rules take a `Func<T, CancellationToken, ValueTask<TimeZoneInfo>>` that runs on every validation, because FastEndpoints validators are singletons: pass `(_, ct) => this.Resolve<ITenantTimeZoneProvider>().GetTimeZoneAsync(ct)`. They are asynchronous rules.

**New:** `TimeZoneDataHealthCheck` and `AddBlueprintTimeZoneDataCheck()` (Csag.Blueprint.Web.HealthChecks): Unhealthy when the host cannot resolve `Europe/Zurich`, i.e. the image has no time zone data; registered as `time_zone_data_check`, tagged `ready`.

**New:** `TableViewDataTypes` (Csag.Blueprint.Application) names the `TableViewColumnMetadata.DataType` values.

**New:** `TableViewDataRequest.TimeZone` and `TableViewExportRequest.TimeZone`, an optional IANA id (normally the viewer's browser zone). `TableViewDataRequestValidator` and the new `TableViewExportRequestValidator` reject an id that is not IANA with a 400 validation error. Without it, the tenant zone applies, and UTC when there is none. `TableViewQueryRequest.TimeZone` carries the resolved zone to the executor; `ITableViewExecutor` gains an `ExecuteWithoutPaginationAsync` overload with a `TimeZoneInfo? timeZone`.

**Breaking:** `TableViewColumnMetadata.DataType` reports `localDateTime` for a `DateTime` column and `instant` for a `DateTimeOffset` column; only `DateOnly` columns still report `date`.

**Breaking:** `BlueprintJsonOptions` registers `LocalDateTimeJsonConverter` (Csag.Blueprint.Application) for `DateTime` and `DateTime?`, on the wire and in JSON the packages persist. It writes `yyyy-MM-ddTHH:mm:ss` (no fraction, no offset, whatever the `Kind`) and reads `yyyy-MM-ddTHH:mm:ss` or `yyyy-MM-ddTHH:mm` with `DateTimeKind.Unspecified`. A value with a UTC offset or `Z`, a fraction, or any other format is refused with a 400 naming the field ("…without a UTC offset or 'Z'…"). `DateTimeOffset` is unchanged. None of the packages' own contracts used `DateTime`.

**Breaking:** `DateRange` filters take calendar dates only (`yyyy-MM-dd,yyyy-MM-dd`, either side optional) and mean whole days per kind: `date` matches `[from, to]` inclusive; `localDateTime` matches `>= from 00:00` and `< (to + 1 day) 00:00` on the reading; `instant` matches `>= start of from` and `< start of (to + 1 day)` in the effective zone. A one-day filter on an instant or local reading column now covers the whole day instead of only 00:00:00, and full timestamps as range bounds are no longer accepted (the filter is ignored, like any malformed value). Temporal values are parsed as ISO 8601 only, never in the server's culture or zone: an `Equals` value on a `DateOnly` column is `yyyy-MM-dd`, on a `DateTime` column `yyyy-MM-ddTHH:mm[:ss]` without an offset, and on a `DateTimeOffset` column either carries an offset or `Z` or is read as a local reading in the effective zone.

**Breaking:** `ITableViewDefinition.GetFilterExpression` takes a `TimeZoneInfo timeZone` third parameter. `TableViewDefinition` keeps the two-argument overload, which evaluates instant filters in UTC, so definition tests keep compiling.

**Breaking:** `TableViewExecutor` takes an `ITenantTimeZoneProvider` in its constructor. It is resolved from DI as before; code that constructs it directly passes one (`new UtcTenantTimeZoneProvider()` in tests).

**Behaviour change:** `TableViewExcelExporter` writes temporal values as real date cells: a `DateOnly` as a date, a local reading as it reads, and an instant converted to the effective zone with its time (previously its UTC date with the time dropped). Number formats follow the request culture's short date and time patterns (instants include seconds) instead of the fixed `dd.MM.yyyy`. The headers of columns with a time name their zone as an IANA id after the column header: an instant column the effective zone (`Created at (Europe/London)`), a local reading column the tenant's zone (`Start (Europe/Zurich)`); date columns are unchanged. `Export` takes optional `timeZone` (the zone instants are shown in and named by; UTC when null), `placeTimeZone` (named by local reading columns; none when null) and `culture` (current culture when null) parameters; `TableViewExportEndpointBase` passes the effective zone, the tenant zone and `CultureInfo.CurrentCulture`.

**Behaviour change:** The OpenAPI document describes a `DateTime` as `type: string, format: local-date-time, pattern: ^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$` (instead of `format: date-time`), and a `TimeSpan` as a string with the pattern of its constant format `[-][d.]hh:mm:ss[.fffffff]`, without `format: duration` (`TemporalSchemaProcessor`, registered by `AddSwaggerDocumentation`).

**Fix:** The `IAuditable.CreatedAt` / `UpdatedAt` docs name the registered `TimeProvider` as the clock the interceptor stamps with.

No EF model change: no migration is needed.

Consumer migration (`csag-blueprint-web`):

- Frontend `components/table-view/cells.tsx` switches on `dataType`: add `localDateTime` (format the reading as it reads, labelled with the place zone when the viewer's differs) and `instant` (format in the viewer's zone); until then both fall through to raw text. Send the viewer's zone (`Intl.DateTimeFormat().resolvedOptions().timeZone`) as `timeZone` on table view data and export requests.
- Every client that sends a `DateTime` must send `yyyy-MM-ddTHH:mm[:ss]` without an offset or `Z`. Today's form-kit `DateTimeInputField` appends the browser offset, so the Example `TestEndpoint` (`DateTimeValue`, `OptionalDateTime`) answers 400 until the form sends local readings; its server-side `new DateTime(..., DateTimeKind.Utc)` example still serializes, without the `Z`.
- A `DateTime` used as an instant in a DTO loses its meaning on the wire: change it to `DateTimeOffset`.
- Regenerate the OpenAPI spec and the Orval client: `DateTime` properties change format and `TimeSpan` properties lose `format: duration`; the generated TypeScript types stay `string`.
- Implement `ITenantTimeZoneProvider` over the tenant's `TimeZoneId` and register it; add `AddBlueprintTimeZoneDataCheck()` to the health checks.
- Use `MustNotBeAfterToday` instead of comparisons with `DateOnly.FromDateTime(DateTime.UtcNow)`, and `MustExistInTimeZone` on local readings.
