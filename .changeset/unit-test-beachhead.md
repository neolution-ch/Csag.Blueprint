---
---

Add the first unit-test project (`Csag.Blueprint.Infrastructure.Tests`) and wire `dotnet test` into CI. Test-infrastructure and build-config only — the test packages added to `Directory.Packages.props` are not referenced by any shipped package, so there is no consumer-facing change and no version bump.
