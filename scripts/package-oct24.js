const fs = require('fs');
const path = require('path');
const os = require('os');
const crypto = require('crypto');
const {execFileSync} = require('child_process');
const repo = path.resolve(__dirname, '..');
const [baseArg, downloadsArg, outputArg] = process.argv.slice(2);
if (!baseArg || !downloadsArg || !outputArg) throw new Error('Usage: node scripts/package-oct24.js <custom-1.4.6-r1.zip> <official-downloads> <output.zip>');
const base = path.resolve(baseArg), downloads = path.resolve(downloadsArg), output = path.resolve(outputArg);
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const baseHash = 'df8f6ba49864de78adc18e2236290121fcd2bd397280f9bb0a9f48e732738941';
if (sha(base) !== baseHash) throw new Error('Unexpected 1.4.6-r1 base');
if (fs.existsSync(output)) throw new Error('Output exists');
const assets = [
  ['mmsource-2.0.0-git1483-windows.zip', '5a5b61413eeb4c204050d7cca83d28c5c707d9edd5e74d4fc9f96cde147c5341'],
  ['counterstrikesharp-with-runtime-windows-1.0.378.zip', '6784cc463e8a85e5e70d41c4190601ac4d93295ae7ebdd1e12b6bb92f861ae02'],
  ['BotController-MM-windows-0.7.2.zip', '378fc882a2f6c00f227164d6536913c5acc5a7b479626fda0629fbeeb53e69d4'],
  ['BotController-CSS-API-0.7.2.zip', 'ba9aae741714a7ec21db54407e1b32fb349dabdbfef6675398aaf9c4502bcf89'],
  ['BotHider-Windows-0.5.2.zip', 'dc82dcfce0b4d56f199e7c52a3f336345ebe4a9ef81f2a422d8c111528e0ddc5'],
  ['BotVision-Windows-0.3.1.zip', 'c2ddf7270b58e59a9ced0ca25825b3d95b6ea74c514537c2d8c3ae7e51be42b8'],
  ['BotAI.zip', '72df739ec5ba84358e1f40013524ac34230f5dc35bf8e130666450c40456bfa4', 'BotAI'],
  ['BotState.zip', 'd67647d3c00a686844c07b01533d57315c28bf4db2e79cdb2b2c521d589ac53b', 'BotState'],
  ['BotAimImprover.zip', '6621475aa789c4cdf8a3c2dea2973980fa743c7736cf3a5825b5e70aa3d4ae6e', 'BotAimImprover'],
  ['NadeSystem.zip', 'e5d4a23e4c04f234e73afeba306706d43a6e46815b80bd9dce05513a2896a020', 'NadeSystem']
];
for (const [file, expected] of assets) if (sha(path.join(downloads, file)) !== expected) throw new Error('Checksum mismatch: ' + file);
const stage = fs.mkdtempSync(path.join(os.tmpdir(), 'cs2-oct24-stage-'));
const unzip = (file, root = stage, entries = []) => execFileSync('unzip', ['-oq', file, ...entries, '-d', root]);
unzip(base);
function files(root, prefix = '') {
  return fs.readdirSync(root, {withFileTypes: true}).flatMap(e => {
    const p = prefix ? prefix + '/' + e.name : e.name;
    return e.isDirectory() ? files(path.join(root, e.name), p) : [p];
  });
}
const protectedPaths = files(stage).filter(p => ['cfg/', 'overrides/', 'backup/', 'BotEquipmentPanel/', 'addons/counterstrikesharp/plugins/NadeSystem/grenades/'].some(prefix => p.startsWith(prefix)) || p === 'gameinfo.gi');
const protectedHashes = Object.fromEntries(protectedPaths.map(p => [p, sha(path.join(stage, p))]));
for (const relative of [
  'addons/metamod/bin', 'addons/counterstrikesharp/api', 'addons/counterstrikesharp/bin',
  'addons/counterstrikesharp/dotnet', 'addons/counterstrikesharp/plugins/BotControllerImpl',
  'addons/counterstrikesharp/plugins/BotHiderImpl', 'addons/counterstrikesharp/shared/BotControllerApi',
  'addons/counterstrikesharp/shared/BotHiderApi', 'addons/counterstrikesharp/shared/0Harmony',
  'addons/BotController/bin', 'addons/BotHider/bin', 'addons/BotVision/bin'
]) fs.rmSync(path.join(stage, relative), {recursive: true, force: true});
unzip(path.join(downloads, assets[0][0]), stage, [
  'addons/metamod/bin/win64/metamod.2.cs2.dll', 'addons/metamod/bin/win64/server.dll',
  'addons/metamod.vdf', 'addons/metamod_x64.vdf'
]);
for (const [file, , plugin] of assets.slice(1)) unzip(path.join(downloads, file), plugin ? path.join(stage, 'addons/counterstrikesharp/plugins', plugin) : stage);
const signatureOverrides = [
  ['BotController', 'XBribo/CS2-Bot-Controller', 'e53785147e1a7c8e7de1174539526f0ea1912a3c', 'cc443e4289fc5a325f7e472204fd477aab1f73e152a17c4cbc92aea8c1c3c620'],
  ['BotVision', 'XBribo/CS2-Bot-Vision', 'fc7bfe07c36277fffc313a23747ab0302c2925a6', '72347e5892bb2e18e81685a716312e3986bc780fefeec25a050d82aa879161ad']
].map(([module, sourceRepository, commit, expectedHash]) => {
  const source = path.join(repo, 'compat/2026-10-24', module + '-gamedata.json');
  if (sha(source) !== expectedHash) throw new Error('Pinned signature snapshot changed: ' + module);
  const destination = path.join(stage, 'addons', module, 'gamedata.json');
  const previous = JSON.parse(fs.readFileSync(destination, 'utf8'));
  const current = JSON.parse(fs.readFileSync(source, 'utf8'));
  for (const key of Object.keys(previous)) if (!(key in current)) throw new Error('Required gamedata key removed: ' + key);
  fs.copyFileSync(source, destination);
  return {module, source_repository: sourceRepository, commit, sha256: sha(source)};
});
function copyBuild(source, destination) {
  fs.mkdirSync(destination, {recursive: true});
  for (const e of fs.readdirSync(source, {withFileTypes: true})) {
    if (e.name === 'publish' || e.name.endsWith('.pdb')) continue;
    fs.cpSync(path.join(source, e.name), path.join(destination, e.name), {recursive: true});
  }
}
for (const name of ['BotRandomizer', 'GG1MapChooser']) {
  const relative = 'addons/counterstrikesharp/plugins/' + name;
  copyBuild(path.join(repo, relative, 'bin/Release/net10.0'), path.join(stage, relative));
}
copyBuild(path.join(repo, 'addons/counterstrikesharp/shared/MapChooserAPI/bin/Release/net10.0'), path.join(stage, 'addons/counterstrikesharp/shared/MapChooserAPI'));
for (const [p, expected] of Object.entries(protectedHashes)) if (sha(path.join(stage, p)) !== expected) throw new Error('Protected file changed: ' + p);
if (fs.existsSync(path.join(stage, 'addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.custom.json'))) throw new Error('User settings must not be packaged');
fs.copyFileSync(path.join(repo, 'dist/OCT24-COMPAT-NOTES.md'), path.join(stage, 'RELEASE-NOTES.md'));
fs.writeFileSync(path.join(stage, 'release-components.json'), JSON.stringify({
  release: 'cs2-v1.4.6-skin-map-r2', revision_date: '2026-10-24', target_game_version: '1.41.9.2',
  base_sha256: baseHash, in_game_validated: false,
  official_assets: assets.map(([asset, sha256]) => ({asset, sha256})),
  signature_overrides: signatureOverrides, preserved_file_hashes: protectedHashes,
  note: 'Official repair binaries are used directly. No custom Bot or grenade strategies are introduced. Only native gamedata is backported from the pinned upstream commits; newer unreleased native code is not included.'
}, null, 2) + String.fromCharCode(10));
execFileSync(process.env.CS2_DOTNET || 'dotnet', ['run', '--project',
  path.join(repo, 'scripts/ApiSmoke/ApiSmoke.csproj'), '-c', 'Release', '--', stage], {stdio: 'inherit'});
execFileSync(process.execPath, [path.join(repo, 'scripts/generate-uninstall-manifest.js'), stage], {stdio: 'inherit'});
execFileSync('zip', ['-qr', output, '.'], {cwd: stage});
execFileSync('unzip', ['-tq', output], {stdio: 'inherit'});
console.log(JSON.stringify({stage, output, sha256: sha(output)}, null, 2));
