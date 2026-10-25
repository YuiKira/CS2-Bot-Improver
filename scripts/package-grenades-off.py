"""Replace r3's grenade module without relying on mode cfg execution."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
source, output = map(Path, sys.argv[1:])
assert source.resolve() != output.resolve() and not output.exists()
base_hash = hashlib.sha256(source.read_bytes()).hexdigest()
assert base_hash == 'c84421fd7a7bcc582a33c045711406333e96b4d69a4e152b8dd4ffd7fbf0b9f5'
prefix = 'addons/counterstrikesharp/plugins/NadeSystem/'
build = root / 'compat/NadeSystemDisabled/bin/Release/net10.0'
changes = {prefix + name: (build / name).read_bytes()
           for name in ['NadeSystem.dll', 'NadeSystem.deps.json', 'NadeSystem.pdb']}
changes['RELEASE-NOTES.md'] = (root / 'dist/GRENADES-OFF-NOTES.md').read_bytes()
with zipfile.ZipFile(source) as old:
    metadata = json.loads(old.read('release-components.json'))
    metadata['release'] = 'cs2-v1.4.6-skin-map-r4'
    metadata['base_release'] = 'cs2-v1.4.6-skin-map-r3'
    metadata['base_sha256'] = base_hash
    metadata['in_game_validated'] = False
    metadata['grenade_module_override'] = {
        'name': 'NadeSystem (Disabled)', 'version': '1.2.3-disabled.1',
        'source': 'compat/NadeSystemDisabled',
        'sha256': hashlib.sha256(changes[prefix + 'NadeSystem.dll']).hexdigest(),
        'behavior': 'No grenade replay, projectile spawning or original event handlers. Enforce bot_allow_grenades=false each tick. bot_nades always reports off.'}
    metadata['note'] = 'Based on working r2 compatibility binaries with r3 Medium reaction tuning. NadeSystem is replaced by a disabled module; all other plugin binaries remain unchanged.'
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
    assert set(changed) == set(changes)
    print(json.dumps({'changed': changed, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2))
