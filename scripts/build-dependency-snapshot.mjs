#!/usr/bin/env node

/**
 * Transforms the output of `dotnet list package --include-transitive --format json`
 * into a GitHub Dependency Submission API snapshot.
 *
 * Usage:
 *   dotnet list package --include-transitive --format json > deps.json
 *   node scripts/build-dependency-snapshot.js deps.json snapshot.json
 *
 * Required environment variables (set automatically on GitHub Actions):
 *   GITHUB_WORKSPACE  - repository root path
 *   GITHUB_SHA        - commit SHA
 *   GITHUB_REF        - git ref (e.g. refs/heads/main)
 *   GITHUB_WORKFLOW   - workflow name
 *   GITHUB_JOB        - job name
 *   GITHUB_RUN_ID     - workflow run ID
 *
 * @see https://docs.github.com/en/rest/dependency-graph/dependency-submission
 */

import fs from "node:fs";
import path from "node:path";

const DETECTOR = {
  name: "dotnet-list-package",
  version: "1.0.0",
  url: "https://learn.microsoft.com/dotnet/core/tools/dotnet-list-package",
};

function buildPurl(packageId, version) {
  return `pkg:nuget/${packageId}@${version}`;
}

function resolveRelativePath(fullPath, workspaceRoot) {
  if (workspaceRoot && fullPath.startsWith(workspaceRoot)) {
    return fullPath.slice(workspaceRoot.length).replace(/^[/\\]+/, "");
  }
  return fullPath;
}

function buildManifests(projects, workspaceRoot) {
  const manifests = {};

  for (const project of projects) {
    const relPath = resolveRelativePath(project.path, workspaceRoot);
    const resolved = {};

    for (const framework of project.frameworks) {
      for (const pkg of framework.topLevelPackages || []) {
        const purl = buildPurl(pkg.id, pkg.resolvedVersion);
        resolved[purl] = {
          package_url: purl,
          relationship: "direct",
          scope: "runtime",
          dependencies: [],
        };
      }

      for (const pkg of framework.transitivePackages || []) {
        const purl = buildPurl(pkg.id, pkg.resolvedVersion);
        if (!resolved[purl]) {
          resolved[purl] = {
            package_url: purl,
            relationship: "indirect",
            scope: "runtime",
            dependencies: [],
          };
        }
      }
    }

    if (Object.keys(resolved).length > 0) {
      manifests[relPath] = {
        name: relPath,
        file: { source_location: relPath },
        resolved,
      };
    }
  }

  return manifests;
}

function buildSnapshot(manifests) {
  return {
    version: 0,
    sha: process.env.GITHUB_SHA,
    ref: process.env.GITHUB_REF,
    job: {
      correlator: `${process.env.GITHUB_WORKFLOW}_${process.env.GITHUB_JOB}`,
      id: process.env.GITHUB_RUN_ID,
    },
    detector: DETECTOR,
    scanned: new Date().toISOString(),
    manifests,
  };
}

function printSummary(manifests) {
  let totalDirect = 0;
  let totalIndirect = 0;

  for (const [name, manifest] of Object.entries(manifests)) {
    const packages = Object.values(manifest.resolved);
    const direct = packages.filter((p) => p.relationship === "direct").length;
    const indirect = packages.filter(
      (p) => p.relationship === "indirect",
    ).length;
    totalDirect += direct;
    totalIndirect += indirect;
    console.log(
      `  ${name}: ${packages.length} packages (${direct} direct, ${indirect} indirect)`,
    );
  }

  console.log(
    `\nTotal: ${Object.keys(manifests).length} manifests, ${totalDirect + totalIndirect} package entries (${totalDirect} direct, ${totalIndirect} indirect)`,
  );
}

// --- Main ---

const [inputFile, outputFile] = process.argv.slice(2);

if (!inputFile || !outputFile) {
  console.error(
    "Usage: node build-dependency-snapshot.js <deps.json> <snapshot.json>",
  );
  process.exit(1);
}

const deps = JSON.parse(fs.readFileSync(path.resolve(inputFile), "utf8"));
const workspaceRoot = process.env.GITHUB_WORKSPACE
  ? process.env.GITHUB_WORKSPACE + "/"
  : "";

const manifests = buildManifests(deps.projects, workspaceRoot);
const snapshot = buildSnapshot(manifests);

fs.writeFileSync(path.resolve(outputFile), JSON.stringify(snapshot));

console.log("Dependency snapshot built successfully.\n");
printSummary(manifests);
