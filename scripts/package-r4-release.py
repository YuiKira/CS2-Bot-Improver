"""Create the new R4 asset from the already validated upstream 1.4.10 package."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

previous, output = map(Path, sys.argv[1:])
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(previous.read_bytes()) == '31d9a30a6c1e86f7a7ad6cd8e748db44d8aa79a6de051c660829f6e40cc6ca42'
assert not output.exists()
with zipfile.ZipFile(previous) as old:
    files = {name: old.read(name) for name in old.namelist() if not name.endswith('/')}
notes = Path(__file__).resolve().parent.parent / 'dist/REACTION-TUNING-NOTES.md'
files['RELEASE-NOTES.md'] = notes.read_bytes()
metadata = json.loads(files['release-components.json'])
metadata['release'] = 'cs2-v1.4.6-skin-map-r4'
metadata['revision'] = '2026-11-25-upstream-1410-r4'
metadata['previous_r3_asset_sha256'] = sha(previous.read_bytes())
metadata['r3_preserved'] = True
files['release-components.json'] = (json.dumps(metadata, indent=2) + '\n').encode()
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
    for name, data in sorted(files.items()):
        new.writestr(name, data)
with zipfile.ZipFile(output) as new:
    assert new.testzip() is None
    assert set(new.namelist()) == set(files)
    assert new.read('RELEASE-NOTES.md') == notes.read_bytes()
    assert json.loads(new.read('release-components.json'))['release'].endswith('-r4')
print(json.dumps({'sha256': sha(output.read_bytes()), 'files': len(files),
    'size': output.stat().st_size, 'r3_asset_sha256': sha(previous.read_bytes())}, indent=2))
