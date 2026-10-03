const fs = require('fs');
const path = require('path');
const os = require('os');
const crypto = require('crypto');
const {execFileSync} = require('child_process');
const repo = path.resolve(__dirname, '..');
const [baseArg, previousArg, outputArg] = process.argv.slice(2);
if (!baseArg || !previousArg || !outputArg) throw new Error('Usage: node scripts/package-upstream.js <official-v1.4.5.zip> <previous-custom.zip> <output.zip>');
const base = path.resolve(baseArg), previous = path.resolve(previousArg), output = path.resolve(outputArg);
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const baseSha = 'ae37b86533abfe0547c5ad4346d478cd846727509fc092842a81240eb0130140';
if (sha(base) !== baseSha) throw new Error('Unexpected official v1.4.5 asset');
if (fs.existsSync(output)) throw new Error('Output already exists');
const stage = fs.mkdtempSync(path.join(os.tmpdir(), 'cs2-v145-stage-'));
execFileSync('unzip', ['-oq', base, '-d', stage]);
function collect(root, prefix = '') {
  return fs.readdirSync(root, {withFileTypes: true}).flatMap(entry => {
    const relative = prefix ? `${prefix}/${entry.name}` : entry.name;
    return entry.isDirectory() ? collect(path.join(root, entry.name), relative) : [relative];
  });
}
const allowedChanges = new Set(['gameinfo.gi', 'backup/Online/gameinfo.gi', 'backup/WithBots/gameinfo.gi']);
const protectedHashes = Object.fromEntries(collect(stage)
  .filter(file => !allowedChanges.has(file) && !file.startsWith('addons/counterstrikesharp/plugins/BotRandomizer/'))
  .map(file => [file, sha(path.join(stage, file))]));
// Import only our map module/configuration, never legacy Bot dependencies.
execFileSync('unzip', ['-oq', previous, 'cfg/GGMCmaps.json',
  'addons/counterstrikesharp/plugins/GG1MapChooser/*',
  'addons/counterstrikesharp/shared/MapChooserAPI/*', '-d', stage]);
function copyBuild(source, destination) {
  fs.mkdirSync(destination, {recursive: true});
  for (const entry of fs.readdirSync(source, {withFileTypes: true})) {
    if (entry.name === 'publish' || entry.name.endsWith('.pdb')) continue;
    fs.cpSync(path.join(source, entry.name), path.join(destination, entry.name), {recursive: true});
  }
}
const randomizer = path.join(stage, 'addons/counterstrikesharp/plugins/BotRandomizer');
fs.rmSync(randomizer, {recursive: true});
for (const plugin of ['BotRandomizer', 'GG1MapChooser']) {
  const relative = `addons/counterstrikesharp/plugins/${plugin}`;
  copyBuild(path.join(repo, relative, 'bin/Release/net10.0'), path.join(stage, relative));
}
copyBuild(path.join(repo, 'addons/counterstrikesharp/shared/MapChooserAPI/bin/Release/net10.0'),
  path.join(stage, 'addons/counterstrikesharp/shared/MapChooserAPI'));
copyBuild(path.join(repo, 'DesktopPanel/bin/Release/net48'), stage);
fs.copyFileSync(path.join(repo, 'dist/UPSTREAM-1.4.5-NOTES.md'), path.join(stage, 'BETA-NOTES.md'));
execFileSync(process.env.CS2_DOTNET || 'dotnet', ['run', '--project',
  path.join(repo, 'scripts/GameInfoTool/GameInfoTool.csproj'), '-c', 'Release', '--',
  path.join(repo, 'dist/gameinfo-online.gi'), stage], {stdio: 'inherit'});
for (const [file, expected] of Object.entries(protectedHashes)) {
  if (sha(path.join(stage, file)) !== expected) throw new Error(`Official file changed: ${file}`);
}
if (fs.existsSync(path.join(randomizer, 'BotRandomizer.custom.json'))) throw new Error('User settings must not be packaged');
fs.writeFileSync(path.join(stage, 'beta-components.json'), JSON.stringify({
  release: 'cs2-v1.4.5-skin-map-beta.1', upstream_version: '1.4.5',
  base_sha256: baseSha, in_game_validated: false, unchanged_official_files: protectedHashes,
  note: 'Official v1.4.5 behavior modules, dependencies, configs and panel are preserved byte-for-byte. Only BotRandomizer and gameinfo are replaced; map voting and equipment tools are added. Legacy vendored Bot sources are not rebuilt.'
}, null, 2) + '\n');
execFileSync(process.execPath, [path.join(repo, 'scripts/generate-uninstall-manifest.js'), stage], {stdio: 'inherit'});
execFileSync('zip', ['-qr', output, '.'], {cwd: stage});
execFileSync('unzip', ['-tq', output], {stdio: 'inherit'});
console.log(JSON.stringify({stage, output, sha256: sha(output)}, null, 2));
