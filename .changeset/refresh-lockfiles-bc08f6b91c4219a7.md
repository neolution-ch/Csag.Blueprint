---
---

chore: refresh lockfiles (transitive dependency update)

Deliberately empty: nothing in this refresh reaches a published package.

- `pnpm-lock.yaml` moved only the `@changesets/*` release tooling. The npm
  manifests under `packages/` are version shells that declare no dependencies,
  so the pnpm graph never reaches a `.nupkg`.
- `packages/Csag.Blueprint.Infrastructure/packages.lock.json` moved only the
  internal `Csag.Blueprint.Domain` project reference, from `[0.1.1, )` to
  `[0.1.3, )`. The lock file is a restore-time artifact that is not packed, and
  the nuspec dependency version comes from the referenced project's `<Version>`,
  which is already 0.1.3.
