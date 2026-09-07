import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

// Run changeset version first
execSync("pnpm exec changeset version", { stdio: "inherit" });

// Find all packages with both package.json and .csproj
const repoRoot = path.resolve(import.meta.dirname, "..");
const packagesDir = path.join(repoRoot, "packages");
for (const dir of fs.readdirSync(packagesDir)) {
  const pkgJsonPath = path.join(packagesDir, dir, "package.json");
  const projDir = path.join(packagesDir, dir);

  if (!fs.existsSync(pkgJsonPath)) continue;

  const pkgJson = JSON.parse(fs.readFileSync(pkgJsonPath, "utf8"));
  const version = pkgJson.version;

  // Find .csproj files in this directory
  const csprojFiles = fs.readdirSync(projDir).filter((f) => f.endsWith(".csproj"));
  for (const csproj of csprojFiles) {
    const csprojPath = path.join(projDir, csproj);
    let content = fs.readFileSync(csprojPath, "utf8");

    if (content.includes("<Version>")) {
      content = content.replace(/<Version>[^<]*<\/Version>/g, `<Version>${version}</Version>`);
    } else {
      // Add Version to the first PropertyGroup
      content = content.replace(
        /<PropertyGroup>/,
        `<PropertyGroup>\n    <Version>${version}</Version>`
      );
    }

    fs.writeFileSync(csprojPath, content);
    console.log(`Updated ${csproj} to version ${version}`);
  }
}

// Re-resolve the NuGet lock files so their internal ProjectReference ranges match
// the versions written above. Without this every release leaves the lock files
// recording the previous version, and the scheduled "Refresh lockfiles" job picks
// that drift up as though it were a real dependency change.
//
// `--force-evaluate` is required: a plain restore accepts the stale range as
// still valid and leaves the lock files untouched. It is safe here because every
// version in Directory.Packages.props is an exact pin and NuGet resolves the
// lowest applicable version, so nothing but the project references can move.
function dotnetAvailable() {
  try {
    execSync("dotnet --version", { stdio: "ignore" });
    return true;
  } catch {
    return false;
  }
}

if (dotnetAvailable()) {
  execSync("dotnet restore --force-evaluate", { stdio: "inherit", cwd: repoRoot });
} else {
  // This script is also run by hand via `pnpm version-packages`; a missing SDK
  // should not hard-fail it. A restore that actually fails still throws.
  console.warn("dotnet not found - skipped 'dotnet restore --force-evaluate'.");
  console.warn("packages.lock.json project-reference ranges may be left stale.");
}
