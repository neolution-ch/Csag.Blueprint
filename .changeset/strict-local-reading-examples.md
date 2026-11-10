---
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-web": patch
---

OpenAPI examples refuse a `DateTime` that is not a local reading, as responses do

**Breaking:** `LocalDateTimeFormat.Format` (Csag.Blueprint.Application) throws an `InvalidOperationException` for a `DateTime` whose `Kind` is `Utc` or `Local`. `LocalDateTimeJsonConverter` and the OpenAPI example converter both format through it, so an endpoint whose `ExampleRequest` holds such a value (`DateTime.UtcNow`, `new DateTime(..., DateTimeKind.Utc)`) now fails the OpenAPI document instead of showing an instant as a reading without its `Z`. Build example readings with `DateTimeKind.Unspecified`.
