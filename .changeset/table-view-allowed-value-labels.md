---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Serve table view allowed values with human-readable, localizable labels

Enum columns sent their raw member names (`CreditCard`, `FourSeater`) as the only text a frontend had for its filter options, and there was no way to translate them. Each allowed value now carries a label next to the value that filters match on.

**Breaking:** `TableViewColumnMetadata.AllowedValues` is now `IReadOnlyList<TableViewAllowedValue>?` instead of `IEnumerable<string>?`. On the wire each entry is `{ "value": "CreditCard", "label": "Credit card" }` rather than a bare string. Filtering is unchanged: `Enum` and `In` filters still match on `value`.

**New:** `TableViewAllowedValue` (Csag.Blueprint.Application), an immutable record with `Value`, `Label`, and two members that are never serialized: `LabelKey`, a translation key resolved at request time like `DisplayNameKey`, and `IsLabelKeyOptional`, which marks a key whose missing translation is expected.

**Behaviour change:** Enum columns derive one allowed value per member: the member name as `Value`, the humanized member name as `Label` (`CreditCard` → "Credit card", `SMSCode` → "SMS code", `IBAN` → "IBAN", `Top10Items` → "Top 10 items"), and `Enums.{EnumTypeName}.{MemberName}` as an optional `LabelKey`. An application translates a label by adding that key to its translation catalog (for example a nested `Enums.PaymentMethod` class in `TranslationDefaults`); a member without a translation keeps the humanized label, and nothing is logged. Keys use the enum's simple type name, so two enums with the same name share keys.

**New:** `TableViewColumnDefinition` gains `WithAllowedValues(IEnumerable<TableViewAllowedValue>)` for full control over values, labels and keys, and `WithValueLabelKeyPrefix(string prefix)`, which sets every current allowed value's key to `{prefix}.{Value}`. Keys set either way are expected to exist: a missing one is logged as a warning and the definition's label is served, as for display names. `WithValueLabelKeyPrefix` throws `InvalidOperationException` on a column without allowed values. `WithAllowedValues(IEnumerable<string>)` and `Filterable(operator, allowedValues)` keep taking plain strings, served with the string itself as the label and no key; `WithAllowedValues(IEnumerable<string>)` now rejects `null`.

**Behaviour change:** `StringLocalizerTableViewMetadataLocalizer` resolves allowed-value label keys along with display names and descriptions. A column whose labels change is returned as a copy with a new allowed-values list; the definition's list and values are never modified.

**Behaviour change:** `TableViewExcelExporter` writes a text or enum cell whose value matches one of the column's allowed values as that value's label, so exported enum columns show the same (localized) text as the table.

Consumer migration: frontends read `allowedValues[i].value` for filter input and `allowedValues[i].label` for display, and regenerate any API client types. Server code that read `AllowedValues` as strings selects `.Value`. To translate enum labels, add `Enums.{EnumType}.{Member}` keys to the translation catalog, or point a column at existing keys with `WithValueLabelKeyPrefix`.
