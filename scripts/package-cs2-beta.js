// Requires the previous skin/map release, official dependency ZIPs and Release builds.
const fs = require('fs');
const path = require('path');
const os = require('os');
const crypto = require('crypto');
const {execFileSync} = require('child_process');
const repo = path.resolve(__dirname, '..');
const [baseArg, downloadsArg, outputArg] = process.argv.slice(2);
if (!baseArg || !downloadsArg || !outputArg) {
  throw new Error('Usage: node scripts/package-cs2-beta.js <previous-release.zip> <downloads> <output.zip>');
}
const base = path.resolve(baseArg);
const downloads = path.resolve(downloadsArg);
const output = path.resolve(outputArg);
if (base === output) throw new Error('Base and output must be different files');
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const dependencies = [
  ['mmsource-2.0.0-git1472-windows.zip', '2bb55926a05cb156ee72d2e67497e3b3d15df561f19a20b2173bafa5fd721a30'],
  ['counterstrikesharp-with-runtime-windows-1.0.376.zip', '4f2777545a0940351450440288e7487509d539f770e66cf1c30cc2419084c5f8'],
  ['BotController-MM-windows-0.7.0.zip', '75d5885fe8dbbf55773bc044740b08ce269afdd57007175c71f28204579ec743'],
  ['BotController-CSS-API-0.7.0.zip', '5aa0ad73afa617e5c6b61f124d4af0719a972acf642c9dbe34ddaae2ae21ea35'],
  ['BotHider-Windows-0.5.1.zip', '9988286b9c6c849b2a75b5b252ee40734ff430c2a82ab56bc852866448cc507b'],
  ['BotVision-Windows-0.3.0.zip', '04fb6ebc8c1554a96fbdea1029e276785cda015e72ed4e9c489e2c08d8516618']
];
const baseSha = '370addbcb3e543d0d792ba18a4aecda26baa40730e8495fd01feac35638090f5';
if (sha(base) !== baseSha) throw new Error('Unexpected base release');
for (const [name, expected] of dependencies) {
  if (sha(path.join(downloads, name)) !== expected) throw new Error(`Checksum mismatch: ${name}`);
}
if (fs.existsSync(output)) throw new Error('Output already exists');
const stage = fs.mkdtempSync(path.join(os.tmpdir(), 'cs2-beta-stage-'));
const unzip = (file, entries = []) => execFileSync('unzip', ['-oq', file, ...entries, '-d', stage]);
unzip(base);
const protectedFiles = ['BotAI/BotAI.dll', 'BotState/BotState.dll', 'NadeSystem/NadeSystem.dll', 'BotAimImprover/BotAimImprover.dll', 'BotBuy/BotBuy.dll'];
const protectedHashes = Object.fromEntries(protectedFiles.map(file => [file, sha(path.join(stage, 'addons/counterstrikesharp/plugins', file))]));
// Remove only known generated staging directories to prevent stale dependency copies.
for (const relative of [
  'publish', 'addons/metamod/bin', 'addons/counterstrikesharp/api',
  'addons/counterstrikesharp/dotnet', 'addons/counterstrikesharp/bin',
  'addons/BotController', 'addons/BotHider', 'addons/BotVision',
  'addons/counterstrikesharp/plugins/BotControllerImpl',
  'addons/counterstrikesharp/plugins/BotHiderImpl',
  'addons/counterstrikesharp/shared/BotControllerApi',
  'addons/counterstrikesharp/shared/BotHiderApi',
  'addons/counterstrikesharp/shared/0Harmony'
]) fs.rmSync(path.join(stage, relative), {recursive: true, force: true});
unzip(path.join(downloads, dependencies[0][0]), [
  'addons/metamod/bin/win64/metamod.2.cs2.dll',
  'addons/metamod/bin/win64/server.dll', 'addons/metamod.vdf', 'addons/metamod_x64.vdf'
]);
for (const [name] of dependencies.slice(1)) unzip(path.join(downloads, name));
function copyBuild(source, target, exclude = new Set()) {
  fs.mkdirSync(target, {recursive: true});
  for (const entry of fs.readdirSync(source, {withFileTypes: true})) {
    if (exclude.has(entry.name) || entry.name.endsWith('.pdb')) continue;
    fs.cpSync(path.join(source, entry.name), path.join(target, entry.name), {recursive: true});
  }
}
for (const plugin of ['BotRandomizer', 'GG1MapChooser']) {
  const relative = `addons/counterstrikesharp/plugins/${plugin}`;
  copyBuild(path.join(repo, relative, 'bin/Release/net10.0'), path.join(stage, relative));
}
copyBuild(path.join(repo, 'addons/counterstrikesharp/shared/MapChooserAPI/bin/Release/net10.0'),
  path.join(stage, 'addons/counterstrikesharp/shared/MapChooserAPI'));
copyBuild(path.join(repo, 'DesktopPanel/bin/Release/net48'), stage, new Set(['publish']));
fs.copyFileSync(path.join(repo, 'dist/BETA-NOTES.md'), path.join(stage, 'BETA-NOTES.md'));
for (const [file, expected] of Object.entries(protectedHashes)) {
  if (sha(path.join(stage, 'addons/counterstrikesharp/plugins', file)) !== expected) throw new Error(`Upstream behavior binary changed: ${file}`);
}
if (fs.existsSync(path.join(stage, 'addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.custom.json'))) {
  throw new Error('Package must not contain user cosmetic settings');
}
fs.writeFileSync(path.join(stage, 'beta-components.json'), JSON.stringify({
  release: 'cs2-20260924-windows-beta.1', in_game_validated: false,
  base_sha256: baseSha,
  dependencies: dependencies.map(([asset, sha256]) => ({asset, sha256})),
  unchanged_upstream_behavior_binaries: protectedHashes,
  note: 'Native dependencies and their managed providers use official release binaries, not the older vendored source snapshots. Bot AI and grenade behavior are unchanged. BotController 0.7.0 uses ABI 22; older controller recordings are incompatible.'
}, null, 2) + '\n');
execFileSync(process.execPath, [path.join(repo, 'scripts/generate-uninstall-manifest.js'), stage], {stdio: 'inherit'});
execFileSync('zip', ['-qr', output, '.'], {cwd: stage});
execFileSync('unzip', ['-tq', output], {stdio: 'inherit'});
console.log(JSON.stringify({stage, output, sha256: sha(output)}, null, 2));
