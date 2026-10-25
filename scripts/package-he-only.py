"""Replace r4 blanket disable with selective HE filtering."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
source, output = map(Path, sys.argv[1:])
assert source.resolve() != output.resolve() and not output.exists()
base_hash = hashlib.sha256(source.read_bytes()).hexdigest()
assert base_hash == '5f8338ce709218374b170dc10c00c65ddfd91749ce6b856a0074007ea3fe18c6'
prefix = 'addons/counterstrikesharp/plugins/NadeSystem/'
build = root / 'compat/NadeSystemHEOnly/bin/Release/net10.0'
changes = {prefix + name: (build / name).read_bytes()
           for name in ['NadeSystem.dll', 'NadeSystem.deps.json', 'NadeSystem.pdb']}
changes['RELEASE-NOTES.md'] = (root / 'dist/HE-ONLY-NOTES.md').read_bytes()
with zipfile.ZipFile(source) as old:
    for mode in ['normal', 'ffa', 'rush']:
        path = 'cfg/my_bot_' + mode + '_config.cfg'
        data = old.read(path)
        assert b'bot_nades off' in data
        data = data.replace(b'bot_nades off', b'bot_nades normal')
        changes[path] = data
        (root / path).write_bytes(data)
    metadata = json.loads(old.read('release-components.json'))
    metadata['custom_tuning']['bot_nades'] = 'normal (HE blocked)'
    metadata['release'] = 'cs2-v1.4.6-skin-map-r5'
    metadata['base_release'] = 'cs2-v1.4.6-skin-map-r4'
    metadata['base_sha256'] = base_hash
    metadata['in_game_validated'] = False
    metadata['grenade_module_override'] = {
        'name': 'NadeSystem', 'version': '1.2.3-nohe.1',
        'source': 'compat/NadeSystemHEOnly',
        'sha256': hashlib.sha256(changes[prefix + 'NadeSystem.dll']).hexdigest(),
        'behavior': 'Block HE only at database, replay and spawn entry points. Original non-HE behavior restored; default mode normal. Native throws remain disabled as upstream.'}
    metadata['note'] = 'Based on working r2 compatibility binaries with r3 Medium reaction tuning. Only HE is filtered; original non-HE grenade behavior is restored. All other plugin binaries remain unchanged.'
    for field in ['preserved_file_hashes', 'unchanged_official_files']:
        for path in changes:
            metadata.get(field, {}).pop(path, None)
    changes['release-components.json'] = (json.dumps(metadata, indent=2) + chr(10)).encode()
    assert set(changes).issubset(old.namelist())
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
        for info in old.infolist():
            new.writestr(info, changes.get(info.filename, old.read(info.filename)))
with zipfile.ZipFile(source) as old, zipfile.ZipFile(output) as new:
    assert new.testzip() is None
    assert old.namelist() == new.namelist()
    changed = [path for path in old.namelist() if old.read(path) != new.read(path)]
    assert set(changed) == {path for path, data in changes.items() if old.read(path) != data}
    print(json.dumps({'changed': changed, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2))
