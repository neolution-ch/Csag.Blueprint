# CSAG Blueprint Packages

Shared `Csag.Blueprint.*` NuGet packages for applications built on the [CSAG Blueprint](https://github.com/neolution-ch/csag-blueprint-web). Extracted from that repository — the pre-extraction git history lives there.

## Packages

| Package | Description |
| --- | --- |
| [`Csag.Blueprint.Domain`](packages/Csag.Blueprint.Domain/README.md) | Core domain contracts, entities, and Identity base types |
| [`Csag.Blueprint.Application`](packages/Csag.Blueprint.Application/README.md) | Application-layer abstractions, claim types, tenant context, table-view system |
| [`Csag.Blueprint.Infrastructure`](packages/Csag.Blueprint.Infrastructure/README.md) | `BlueprintDbContext`, interceptors, EF configurations, tenancy, localization |
| [`Csag.Blueprint.Web`](packages/Csag.Blueprint.Web/README.md) | ASP.NET Core middleware, validated options, security, FastEndpoints extensions |
| [`Csag.Blueprint.Testing`](packages/Csag.Blueprint.Testing/README.md) | Testcontainers integration test base classes, AutoFixture support |
| [`Csag.Blueprint.SourceGenerators`](packages/Csag.Blueprint.SourceGenerators/README.md) | Roslyn incremental source generator for translation keys |

All packages are published to [nuget.org](https://www.nuget.org/packages?q=Csag.Blueprint) and **versioned in lockstep**: every release bumps all six packages to the same version, and consumers must always reference all `Csag.Blueprint.*` packages at the same version (this avoids diamond-dependency conflicts between the layered packages).

The package architecture (generic type strategy, app-owned migrations, upgrade rules) is documented in the blueprint repository: [docs/architecture/PACKAGES.md](https://github.com/neolution-ch/csag-blueprint-web/blob/main/docs/architecture/PACKAGES.md).

## Local development

```bash
dotnet build
dotnet pack -c Release -o ./nupkgs
```

Requires the .NET SDK pinned in [global.json](global.json). Restore uses NuGet lockfiles (`packages.lock.json`); CI restores with `--locked-mode`, so run `dotnet restore` after changing dependencies to refresh the lockfiles.

## Release process

This repository follows the [neolution-ch release playbook](https://github.com/neolution-ch/release-playbook) (Changesets, NuGet variant):

1. Every PR that changes a package needs a changeset: run `npx changeset`, pick any one package (all six are a fixed group — they bump together), choose the bump type, and describe the change. CI blocks PRs without one.
2. On merge to `main`, the Release workflow maintains a **"chore: version packages"** PR that accumulates pending changesets (versions synced into the `.csproj` files via `scripts/sync-versions.js`).
3. Merging that PR creates git tags and GitHub Releases; the **NuGet Publish** workflow then packs and pushes all packages to nuget.org.

Conventions:

- The packages are on a `0.x` version: breaking changes are declared as **minor** changesets, fixes as **patch**. Prerelease channels (`alpha`/`beta`/`rc`) are reserved for staging `1.0.0` later.
- Every release that changes the EF model must call it out in the changeset so consumers know to generate a migration (see the upgrade rules in PACKAGES.md).
- Dependabot PRs get changesets generated automatically. **Manual** dependency bumps in `Directory.Packages.props` do not trip the changeset check — add a changeset yourself.
