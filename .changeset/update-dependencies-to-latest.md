---
"@neolution-ch/csag-blueprint-domain": patch
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
"@neolution-ch/csag-blueprint-web": patch
"@neolution-ch/csag-blueprint-testing": patch
"@neolution-ch/csag-blueprint-source-generators": patch
---

Update dependencies to their latest versions and regenerate the lock files so
transitive dependencies are refreshed as well. Microsoft.ApplicationInsights
stays on the 2.x branch.

Consuming these packages now requires the .NET SDK 10.0.4xx feature band
(10.0.400 or newer). Csag.Blueprint.SourceGenerators is built against Roslyn
5.9.0, which only ships in that band; on an older SDK the compiler skips the
translation-key generator with a CS9057 warning and the generated types go
missing. See the SDK requirement table in the README.
