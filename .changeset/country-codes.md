---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Add a country catalog and a validation rule for country codes

`ICountryCatalog` in `Csag.Blueprint.Application.Abstractions.Services` holds the accepted ISO 3166-1 alpha-2 country codes.

`NagerCountryCatalog` in `Csag.Blueprint.Infrastructure.Countries` implements it with the `Nager.Country` package, so `Csag.Blueprint.Infrastructure` now depends on `Nager.Country` 4.8.0. `AddBlueprintServices` registers it through `AddBlueprintCountries`.

`MustBeCountryCode` in `Csag.Blueprint.Web.Validation` is a FluentValidation rule for a country code field. A null or empty value passes, so the rule fits an optional field.

**When upgrading:**

- To use `MustBeCountryCode`, define the `Validation.InvalidCountry` translation key with a `{FieldName}` placeholder, for example `"{FieldName} must be a valid country"`.
