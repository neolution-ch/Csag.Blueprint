# @neolution-ch/csag-blueprint-source-generators

## 0.3.1

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

## 0.2.0

### Patch Changes

- [#34](https://github.com/neolution-ch/Csag.Blueprint/pull/34) [`338208d`](https://github.com/neolution-ch/Csag.Blueprint/commit/338208d4cb833eadfb2aae7fd0bb48447ad17241) Thanks [@dependabot](https://github.com/apps/dependabot)! - Bump the nuget-ecosystem group with 3 updates
  
  | Package | From | To | Bump |
  |---------|------|----|------|
  | FastEndpoints | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Swagger | 8.2.0 | 8.3.0 | 🟡 minor |
  | FastEndpoints.Testing | 8.2.0 | 8.3.0 | 🟡 minor |

- [#27](https://github.com/neolution-ch/Csag.Blueprint/pull/27) [`92310d6`](https://github.com/neolution-ch/Csag.Blueprint/commit/92310d656e62d5006c5792b0899224d71e5986fa) Thanks [@neotrow](https://github.com/neotrow)! - Harden the translation-key generator's edge-case handling and make its output deterministic:
  
  - A `TranslationDefaults` class in the global namespace now generates compilable sources: the
    generated files omit the namespace declaration instead of emitting the invalid
    `namespace <global namespace>;`.
  - A non-partial `TranslationDefaults` class no longer breaks the consuming build with an
    unexplained `CS0260`; the generator reports warning `CSAGGEN001` and skips generation for
    that class.
  - Multiple `TranslationDefaults` classes in one compilation are still merged first-wins, but the
    merge is now surfaced as warning `CSAGGEN002` naming every participating class.
  - Generated keys, constants, and properties are sorted with ordinal string comparison instead of
    the culture-sensitive default, so the output no longer depends on the build machine's culture.

## 0.1.3

### Patch Changes

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
