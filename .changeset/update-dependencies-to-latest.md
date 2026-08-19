---
"@neolution-ch/csag-blueprint-domain": patch
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
"@neolution-ch/csag-blueprint-web": patch
"@neolution-ch/csag-blueprint-testing": patch
"@neolution-ch/csag-blueprint-source-generators": patch
---

Update all dependencies to their latest versions and regenerate the lock files
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
