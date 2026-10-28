#!/usr/bin/env node

/**
 * Removes transitive pins from Directory.Packages.props once they are no longer needed.
 *
 * A transitive pin is a <PackageVersion> for a package that no project references
 * directly. With CentralPackageTransitivePinningEnabled it raises the version that a
 * dependency pulls in, usually to clear a security advisory. NuGet resolves the lowest
 * version a dependency allows, so a pin stays necessary until the parent package itself
 * requires a fixed version. Nothing notices when that happens, and Dependabot keeps
 * bumping the pin like any other central version.
 *
 * Pins are found from the lock files: every package that some packages.lock.json lists
 * as "CentralTransitive" and none lists as "Direct". Their position in
 * Directory.Packages.props does not matter, because Dependabot adds pins wherever it likes.
 *
 * Each pin is deleted in turn, the lock files are re-resolved and the NuGet
 * vulnerability scan runs. The deletion is kept when the scan reports no vulnerable
 * package version beyond those it reported with every pin in place. Otherwise the pin
 * is put back. A pin marked KeepPin="true" is never tried, for pins held for a reason
 * other than an advisory.
 *
 * Usage:
 *   node scripts/prune-transitive-pins.mjs [report.md]
 *
 * Leaves Directory.Packages.props and every packages.lock.json matching the pins that
 * remain. When a report path is given, writes a Markdown report there (the body of the
 * pull request opened by the Refresh lockfiles workflow).
 */

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const repoRoot = path.resolve(import.meta.dirname, "..");
const propsPath = path.join(repoRoot, "Directory.Packages.props");
const reportPath = process.argv[2];

// A whole <PackageVersion ... /> element on its own lines. Attribute values are matched
// as quoted strings so that a ">" inside one (RemoveWhen="... >= 1.2.3") does not end it.
const packageVersionPattern =
  /^[ \t]*<PackageVersion\b((?:[^>"]|"[^"]*")*)\/>[ \t]*\r?\n/gm;

function run(command, args) {
  return execFileSync(command, args, {
    cwd: repoRoot,
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
  });
}

/**
 * Read every packages.lock.json tracked by git. Returns, per lower-cased package id,
 * the dependency types it appears with and the versions it resolves to.
 */
function readLockFiles() {
  const files = run("git", ["ls-files", "*packages.lock.json"])
    .split("\n")
    .filter(Boolean);
  const packages = new Map();

  for (const file of files) {
    const raw = fs
      .readFileSync(path.join(repoRoot, file), "utf8")
      .replace(/^﻿/, "");
    const lock = JSON.parse(raw);
    for (const framework of Object.values(lock.dependencies || {})) {
      for (const [id, entry] of Object.entries(framework)) {
        const key = id.toLowerCase();
        if (!packages.has(key)) {
          packages.set(key, { types: new Set(), versions: new Set() });
        }
        const info = packages.get(key);
        info.types.add(entry.type);
        if (entry.resolved) info.versions.add(entry.resolved);
      }
    }
  }

  return packages;
}

/**
 * Find the transitive pins in Directory.Packages.props: every PackageVersion whose
 * package the lock files show as pinned transitively and never as a direct reference.
 */
function findPins(props, lockPackages) {
  const pins = [];
  for (const match of props.matchAll(packageVersionPattern)) {
    const attributes = Object.fromEntries(
      [...match[1].matchAll(/([\w.]+)\s*=\s*"([^"]*)"/g)].map((a) => [
        a[1].toLowerCase(),
        a[2],
      ]),
    );
    const id = attributes.include;
    const info = id && lockPackages.get(id.toLowerCase());
    if (!info?.types.has("CentralTransitive") || info.types.has("Direct")) {
      continue;
    }
    pins.push({
      id,
      version: attributes.version,
      element: match[0],
      keep: attributes.keeppin?.toLowerCase() === "true",
    });
  }
  return pins;
}

/** Re-resolve every lock file against the current Directory.Packages.props. */
function restore() {
  run("dotnet", ["restore", "--force-evaluate"]);
}

/**
 * Restore exactly what the lock files record, so that the pull request only ever
 * contains changes caused by removing pins.
 */
function restoreLocked() {
  run("dotnet", ["restore", "--locked-mode"]);
}

/**
 * Run the NuGet vulnerability scan over the restored solution. Returns the vulnerable
 * packages as "id@version" keys mapped to their advisory URLs.
 */
function scanVulnerabilities() {
  const report = JSON.parse(
    run("dotnet", [
      "list",
      "package",
      "--vulnerable",
      "--include-transitive",
      "--no-restore",
      "--format",
      "json",
    ]),
  );

  // A scan that could not run reports problems instead of packages. Treating that as
  // "nothing vulnerable" would remove every pin, so stop instead.
  const problems = [
    ...(report.problems || []),
    ...report.projects.flatMap((project) => project.problems || []),
  ];
  if (problems.length > 0) {
    throw new Error(
      `The vulnerability scan reported problems:\n${JSON.stringify(problems, null, 2)}`,
    );
  }

  const vulnerable = new Map();
  for (const project of report.projects) {
    for (const framework of project.frameworks || []) {
      for (const pkg of [
        ...(framework.topLevelPackages || []),
        ...(framework.transitivePackages || []),
      ]) {
        vulnerable.set(
          `${pkg.id}@${pkg.resolvedVersion}`,
          (pkg.vulnerabilities || []).map((v) => v.advisoryurl),
        );
      }
    }
  }
  return vulnerable;
}

function formatVersions(versions) {
  return [...versions].sort().join(", ") || "none";
}

function buildReport(removed, kept) {
  const lines = [
    "## Transitive pin cleanup",
    "",
    "Opened by the **Refresh lockfiles** workflow (`scripts/prune-transitive-pins.mjs`).",
    "Each transitive pin in `Directory.Packages.props` was removed in turn, the lock files",
    "were re-resolved and the NuGet vulnerability scan was run. A removal is kept only when",
    "the scan reports nothing that it did not already report with every pin in place.",
    "",
    "Removing a pin also removes it from the dependencies of every published package that",
    "resolved it. When that is the case, the changeset of this PR is a patch.",
    "",
  ];

  if (removed.length > 0) {
    lines.push("### Removed", "");
    lines.push("| Package | Pinned at | Resolves to without the pin |");
    lines.push("|---------|-----------|-----------------------------|");
    for (const pin of removed) {
      lines.push(
        `| ${pin.id} | ${pin.version} | ${formatVersions(pin.resolvedAfter)} |`,
      );
    }
    lines.push("");
  }

  if (kept.length > 0) {
    lines.push("### Still needed", "");
    lines.push("| Package | Pinned at | Reason |");
    lines.push("|---------|-----------|--------|");
    for (const pin of kept) {
      lines.push(`| ${pin.id} | ${pin.version} | ${pin.reason} |`);
    }
    lines.push("");
  }

  return lines.join("\n");
}

// Main
restoreLocked();
const original = fs.readFileSync(propsPath, "utf8");
const lockBefore = readLockFiles();
const pins = findPins(original, lockBefore);
console.log(
  pins.length > 0
    ? `Transitive pins: ${pins.map((p) => `${p.id} ${p.version}`).join(", ")}`
    : "No transitive pins found",
);

const removed = [];
const kept = [];
let props = original;
let lockFilesMatchProps = true;

const candidates = pins.filter((pin) => {
  if (pin.keep) kept.push({ ...pin, reason: 'Marked `KeepPin="true"`' });
  return !pin.keep;
});

if (candidates.length > 0) {
  const baseline = scanVulnerabilities();

  for (const pin of candidates) {
    console.log(`Trying without ${pin.id} ${pin.version}`);
    const withoutPin = props.replace(pin.element, "");
    fs.writeFileSync(propsPath, withoutPin, "utf8");

    let reason;
    try {
      restore();
      const introduced = [...scanVulnerabilities()].filter(
        ([key]) => !baseline.has(key),
      );
      if (introduced.length > 0) {
        reason = `Without it the scan reports ${introduced
          .map(([key, urls]) => `${key} (${urls.join(", ")})`)
          .join(", ")}`;
      }
    } catch (error) {
      console.log(String(error.stdout ?? "").slice(-4000));
      reason = `Restore or scan failed without it: ${error.message.split("\n")[0]}`;
    }

    if (reason) {
      console.log(`  keeping it. ${reason}`);
      fs.writeFileSync(propsPath, props, "utf8");
      kept.push({ ...pin, reason });
      lockFilesMatchProps = false;
    } else {
      console.log("  removed");
      props = withoutPin;
      removed.push(pin);
      lockFilesMatchProps = true;
    }
  }

  // The last attempt may have been rolled back after its restore rewrote the lock files.
  if (!lockFilesMatchProps) restore();

  const lockAfter = readLockFiles();
  for (const pin of removed) {
    pin.resolvedAfter =
      lockAfter.get(pin.id.toLowerCase())?.versions ?? new Set();
  }
}

if (reportPath) {
  fs.writeFileSync(reportPath, buildReport(removed, kept), "utf8");
}
console.log(`Removed ${removed.length} pin(s), kept ${kept.length}.`);
