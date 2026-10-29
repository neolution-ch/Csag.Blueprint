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
 * is put back, as it is when restore fails without it. Any other failure, such as a scan
 * that cannot run, puts every file back and stops the script with an error. A pin marked
 * KeepPin="true" is never tried, for pins held for a reason other than an advisory.
 *
 * Usage:
 *   node scripts/prune-transitive-pins.mjs [--dry-run] [report.md]
 *
 * Leaves Directory.Packages.props and every packages.lock.json matching the pins that
 * remain. With --dry-run it tries the pins the same way and then puts both back as they
 * were, so only the output says what a normal run would remove. When a report path is
 * given, writes a Markdown report there (the body of the pull request opened by the
 * Refresh lockfiles workflow, and its run summary).
 */

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const repoRoot = path.resolve(import.meta.dirname, "..");
const propsPath = path.join(repoRoot, "Directory.Packages.props");
const args = process.argv.slice(2);
const dryRun = args.includes("--dry-run");
const reportPath = args.find((arg) => !arg.startsWith("--"));

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

/** Every packages.lock.json tracked by git, relative to the repository root. */
function lockFilePaths() {
  return run("git", ["ls-files", "*packages.lock.json"])
    .split("\n")
    .filter(Boolean);
}

/**
 * Read every packages.lock.json tracked by git. Returns, per lower-cased package id,
 * the dependency types it appears with and the versions it resolves to.
 */
function readLockFiles() {
  const packages = new Map();

  for (const file of lockFilePaths()) {
    const raw = fs
      .readFileSync(path.join(repoRoot, file), "utf8")
      .replace(/^\uFEFF/, "");
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
 * Write Directory.Packages.props and the lock files back with the given contents, and
 * restore so that obj/ matches them again.
 */
function putBack(propsContent, lockFiles) {
  fs.writeFileSync(propsPath, propsContent, "utf8");
  for (const [file, content] of lockFiles) {
    fs.writeFileSync(path.join(repoRoot, file), content);
  }
  restoreLocked();
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

function buildReport(pins, removed, kept) {
  const lines = [
    dryRun ? "## Transitive pin cleanup (dry run)" : "## Transitive pin cleanup",
    "",
    dryRun
      ? "Dry run of `scripts/prune-transitive-pins.mjs`. Nothing was changed. A normal run removes the pins listed under **Would be removed** and opens a pull request."
      : "Opened by the **Refresh lockfiles** workflow (`scripts/prune-transitive-pins.mjs`).",
    "Each transitive pin in `Directory.Packages.props` was removed in turn, the lock files",
    "were re-resolved and the NuGet vulnerability scan was run. A removal is kept only when",
    "the scan reports nothing that it did not already report with every pin in place.",
    "",
    "Without its pin, a package resolves to the version its parents ask for, which can be",
    "lower than the pin. The table shows that version. Removing a pin also removes it from",
    "the dependencies of every published package that resolved it. When that is the case,",
    "the pull request carries a patch changeset.",
    "",
  ];

  if (pins.length === 0) {
    lines.push("No transitive pins found.", "");
  }

  if (removed.length > 0) {
    lines.push(dryRun ? "### Would be removed" : "### Removed", "");
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
const originalLockFiles = new Map(
  lockFilePaths().map((file) => [
    file,
    fs.readFileSync(path.join(repoRoot, file)),
  ]),
);
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
  try {
    const baseline = scanVulnerabilities();

    for (const pin of candidates) {
      console.log(`Trying without ${pin.id} ${pin.version}`);
      const withoutPin = props.replace(pin.element, "");
      fs.writeFileSync(propsPath, withoutPin, "utf8");

      let reason;
      try {
        restore();
      } catch (error) {
        console.log(String(error.stdout ?? "").slice(-4000));
        reason = `Restore failed without it: ${error.message.split("\n")[0]}`;
      }

      if (!reason) {
        const vulnerable = scanVulnerabilities();
        const introduced = [...vulnerable].filter(
          ([key]) => !baseline.has(key),
        );
        if (introduced.length > 0) {
          reason = `Without it the scan reports ${introduced
            .map(([key, urls]) => `${key} (${urls.join(", ")})`)
            .join(", ")}`;
        }
      }

      if (reason) {
        console.log(`  keeping it. ${reason}`);
        fs.writeFileSync(propsPath, props, "utf8");
        kept.push({ ...pin, reason });
        lockFilesMatchProps = false;
      } else {
        console.log(dryRun ? "  would be removed" : "  removed");
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
  } catch (error) {
    // Anything that fails here, such as a scan that cannot run, says nothing about the
    // pins. Put every file back and stop, so that a partial run never turns into a pull
    // request and a dry run never leaves changes behind.
    console.log(String(error.stdout ?? "").slice(-4000));
    try {
      putBack(original, originalLockFiles);
    } catch {
      // The first error is the one worth reporting.
    }
    throw error;
  }

  if (dryRun) putBack(original, originalLockFiles);
}

if (reportPath) {
  fs.writeFileSync(reportPath, buildReport(pins, removed, kept), "utf8");
}
console.log(
  dryRun
    ? `Dry run: would remove ${removed.length} pin(s), keep ${kept.length}. Nothing was changed.`
    : `Removed ${removed.length} pin(s), kept ${kept.length}.`,
);
