const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

// Run changeset version first
execSync("npx changeset version", { stdio: "inherit" });

// Find all packages with both package.json and .csproj
const packagesDir = path.join(__dirname, "..", "packages");
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
