const fs = require("fs");
const path = require("path");

const root = path.resolve(process.argv[2] || "");
if (!process.argv[2] || !fs.statSync(root).isDirectory()) {
  throw new Error("Usage: node scripts/generate-uninstall-manifest.js <release-directory>");
}

const steamManaged = new Set([
  "gameinfo.gi",
  "cfg/bot_buy.cfg"
]);

function collectFiles(directory, prefix = "") {
  const files = [];
  for (const entry of fs.readdirSync(directory, {withFileTypes: true})) {
    const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (relative === "uninstall-manifest.json") continue;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...collectFiles(fullPath, relative));
    else if (entry.isFile()) files.push(relative);
  }
  return files;
}

const allFiles = collectFiles(root).sort((a, b) => a.localeCompare(b, "en"));
for (const file of allFiles) {
  if (/^cfg\/gamemode_.*\.cfg$/i.test(file)) steamManaged.add(file);
}

const manifest = {
  schema_version: 1,
  product: "CS2-Bot-Improver",
  product_owned_files: allFiles.filter(file => !steamManaged.has(file)).concat("uninstall-manifest.json").sort(),
  runtime_files: [
    "addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.custom.json",
    "addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.custom.backup.json",
    "addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.custom.json.tmp"
  ],
  steam_managed_files: [...steamManaged].sort(),
  restore_files: [
    {source: "backup/Online/gameinfo.gi", destination: "gameinfo.gi"}
  ],
  local_app_data_directories: ["CS2BotImprover"]
};

fs.writeFileSync(
  path.join(root, "uninstall-manifest.json"),
  `${JSON.stringify(manifest, null, 2)}\n`,
  "utf8"
);

console.log(`Wrote uninstall manifest: ${manifest.product_owned_files.length} owned, ${manifest.steam_managed_files.length} Steam-managed files.`);
