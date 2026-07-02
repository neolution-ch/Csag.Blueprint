# Csag.Blueprint.SourceGenerators

## Overview

This package contains a **Roslyn incremental source generator** that scans `TranslationDefaults` class hierarchies at compile time and produces strongly-typed translation registries and DTOs for multilingual support.

## What it generates

Given a `TranslationDefaults` class with nested static classes and string constants, the generator produces three files:

| Generated file              | Contents |
|-----------------------------|----------|
| `TranslationDefaults.g.cs` | A partial class extending `TranslationDefaults` with an `All` property (`IReadOnlyDictionary<string, string>` mapping dot-path keys to English defaults). |
| `TranslationKeys.g.cs`     | A static class mirroring the `TranslationDefaults` hierarchy where each constant holds the dot-separated key path (e.g. `"Validation.EmailRequired"`). Pass these to `IStringLocalizer` for direct database lookup. |
| `TranslationValues.g.cs`   | A sealed `TranslationValues` DTO mirroring the `TranslationDefaults` hierarchy. Each category becomes a nested sealed class with `JsonPropertyName` attributes (camelCase). Includes a static `FromDictionary()` method for populating from a flat key-value dictionary. |

## How it works

1. The generator filters for `ClassDeclarationSyntax` nodes named `TranslationDefaults`.
2. It recursively collects all constant string fields and nested static classes, building dot-separated key paths (e.g., `Validation.EmailRequired`).
3. It builds a tree from the flat entries and emits all three source files.

## Usage

```csharp
// TranslationDefaults.cs — define keys with English text as the const value
public static class Validation
{
    public const string EmailRequired = "Email is required";
}

// Call sites — use the generated TranslationKeys for the localizer
localizer[TranslationKeys.Validation.EmailRequired]
// Localizer does a direct DB lookup by "Validation.EmailRequired".
// Falls back to "Email is required" (from TranslationDefaults.All) if not found.
```
