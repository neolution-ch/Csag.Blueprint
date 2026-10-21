---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-web": minor
---

Table view exports validate pagination and cap unpaginated exports at `MaxExportRows`

`TableViewExportEndpointBase` accepted any `Page` and `PageSize`, and an export without them read every row matching its filters into one workbook, however many there were.

**New:** `TableViewExportRequestValidator` (Csag.Blueprint.Web) applies to every endpoint taking a `TableViewExportRequest`. `Page` and `PageSize` are set both or neither; when set, `Page` is at least 1 and `PageSize` is between 1 and 100, as for `TableViewDataRequest`. Sort columns follow the same rules as on the data request.

**New:** `TableViewExportEndpointBase.MaxExportRows`, a `protected virtual int`, default `10_000`. An export without a page that matches more rows than that is refused with `400 Bad Request` and a ProblemDetails body naming the row count and the cap; it is never truncated. Override it per endpoint, like `ExportFileName`.

**Behaviour change:** the base endpoint reads an unpaginated export through `ITableViewExecutor.ExecuteAsync` as one page of `MaxExportRows` rows, instead of `ExecuteWithoutPaginationAsync`. `ExecuteWithoutPaginationAsync` remains on the interface.

Consumer migration: a client that sent `Page` without `PageSize` (or the reverse), or a `PageSize` above 100, now gets `400` — send both, within the bounds, or neither. An endpoint whose tables legitimately export more than 10,000 rows overrides `MaxExportRows`. Frontends surface the `400` to the user, for example as a hint to narrow the filters.
