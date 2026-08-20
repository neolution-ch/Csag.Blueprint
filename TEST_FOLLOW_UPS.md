# Test-suite follow-ups

Remaining items after the fix PR (which resolved the package bugs surfaced while building the test suites — see the changesets and the flipped pinning tests in that PR's diff).

## In this repo

1. **CI shape decision:** now that the suite includes containerized integration tests, consider splitting `dotnet test` into separate unit and integration jobs (unit feedback stays fast; integration carries the Docker/container startup cost). Currently both run in the single Build, Test & Pack job.
2. **SSH.NET advisory (GHSA-q939-rpr3-3284): resolved upstream** — Testcontainers.MsSql 4.14.0 requires the patched SSH.NET 2026.0.0 and every lock file resolves it; no pin needed. If a future Testcontainers bump ever regresses the range, the NU1903 audit warning will resurface visibly.

## In csag-blueprint-web (the consuming template repo)

3. **Move `parseServerErrors.test.ts`** from `src/Web.Frontend/src/__tests__/` into `src/packages/react-form-kit/__tests__/` before any npm extraction of the package layer; `parseServerErrors.ts` also imports the app-level `isProblemDetails` helper, which must move into the package layer first.
4. **After these packages are released and the web repo's pin is bumped:** thin the web repo's now-duplicated test suites down to their consumer smokes per `docs/architecture/PACKAGES.md` (behavior is guarded upstream; the app keeps composition/wiring coverage, appsettings pins, and app-feature tests).
5. **SigningKey presence enforcement:** with `JwtSettingsValidator` no longer requiring `SigningKey`, the host's runtime-gated check (`AddRuntimeServices` in the template) is the single enforcement point for key presence outside generation mode — keep that check when evolving the template's startup.
6. **Translation seed data:** the translation subsystem now canonicalizes language codes to lowercase; the web repo's migrations/seeders should use lowercase codes (`de-ch`, not `de-CH`) — `SeedTranslation` normalizes automatically, but hand-written queries against `BlueprintTranslations` should assume lowercase rows.
