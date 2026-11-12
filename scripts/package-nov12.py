"""Apply the pinned official CS2 1.41.9.7 CSS fix to the existing custom R3."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
previous, native, api, output = map(Path, sys.argv[1:])
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(previous.read_bytes()) == '30fc5e30db023a7a9995bea928c43ce0705f620e68ba0ad5a5175e03a31c9c20'
assert sha(native.read_bytes()) == '01ed6e0600515b6fafefd84d6803eaabf544796ab789b09ddff250d4b701e85e'
assert sha(api.read_bytes()) == 'e920c8d1511e782d72abcb81f24909bcfe02cc78bb5f33a6e39ffaee4e3d11aa'
stage = output.parent / (output.stem + '-stage')
assert not output.exists() and not stage.exists()
with zipfile.ZipFile(previous) as old:
    files = {n: old.read(n) for n in old.namelist() if not n.endswith('/')}
original = {n: sha(data) for n, data in files.items()}
with zipfile.ZipFile(native) as official:
    for n in ('addons/counterstrikesharp/bin/win64/counterstrikesharp.dll',
              'addons/counterstrikesharp/gamedata/gamedata.json'):
        files[n] = official.read(n)
with zipfile.ZipFile(api) as official:
    prefix = 'net10.0/publish/'
    api_files = {n[len(prefix):]: official.read(n) for n in official.namelist()
                 if n.startswith(prefix) and not n.endswith('/')}
    assert 'CounterStrikeSharp.API.dll' in api_files and len(api_files) >= 40
    for n, data in api_files.items():
        assert '..' not in Path(n).parts
        files['addons/counterstrikesharp/api/' + n] = data
files['RELEASE-NOTES.md'] = (root / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
changed = {n: sha(data) for n, data in files.items() if original.get(n) != sha(data)}
assert all(n.startswith('addons/counterstrikesharp/api/') or n in (
    'addons/counterstrikesharp/bin/win64/counterstrikesharp.dll',
    'addons/counterstrikesharp/gamedata/gamedata.json', 'RELEASE-NOTES.md') for n in changed)
metadata = json.loads(files['release-components.json'])
metadata['revision'] = '2026-11-12-cs2-1.41.9.7-compat'
metadata['in_game_validated'] = False
metadata['compatibility_update'] = {
    'game_version': '1.41.9.7', 'counterstrikesharp': '1.0.380-6 (official CI)',
    'commit': '2eb9a57d800b662e1d728188f7441fa0ede8bf57',
    'ci_run': 43213602663, 'ci_conclusion': 'success',
    'windows_artifact_id': 13942204289, 'api_artifact_id': 13943245050,
    'windows_artifact_sha256': sha(native.read_bytes()),
    'api_artifact_sha256': sha(api.read_bytes()),
    'previous_r3_sha256': sha(previous.read_bytes()),
    'changed_files': changed,
    'optional_botai_patch': 'Bombsite search patch is skipped by original-byte validation on this game build; no guessed displacement is written.'}
# Historical official-base hashes describe the 1.4.8 merge, not this overlay.
metadata['base_148_merge'] = {k: metadata.pop(k) for k in (
    'unchanged_official_files', 'overridden_official_files') if k in metadata}
files['release-components.json'] = (json.dumps(metadata, indent=2) + '\n').encode()
stage.mkdir()
for n, data in files.items():
    p = stage / n
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(data)
subprocess.run(['node', str(root / 'scripts/generate-uninstall-manifest.js'), str(stage)], check=True)
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): new.write(p, p.relative_to(stage).as_posix())
with zipfile.ZipFile(output) as new:
    assert new.testzip() is None
    differences = [n for n, digest in original.items() if sha(new.read(n)) != digest]
    assert set(differences) <= set(changed) | {'release-components.json', 'uninstall-manifest.json'}
    assert set(new.namelist()) == set(files)
    unchanged = len(original) - len(differences)
print(json.dumps({'stage': str(stage), 'sha256': sha(output.read_bytes()),
    'unchanged_files': unchanged, 'changed_files': differences}, indent=2))
