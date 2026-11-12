"""Overlay the two log-confirmed managed startup fixes on the November 12 R3."""
import hashlib
import json
from pathlib import Path
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
previous, output = map(Path, sys.argv[1:])
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(previous.read_bytes()) == '50e78cccd9323cf9efd4b231ef289ac4d1a32f37ba960b0a343adfafb5332f7a'
stage = output.parent / (output.stem + '-stage')
assert not output.exists() and not stage.exists()
with zipfile.ZipFile(previous) as old:
    files = {n: old.read(n) for n in old.namelist() if not n.endswith('/')}
original = {n: sha(data) for n, data in files.items()}
for plugin, project in [('BotAI', 'compat/BotAIReaction'),
                        ('GG1MapChooser', 'addons/counterstrikesharp/plugins/GG1MapChooser')]:
    for ext in ('dll', 'deps.json', 'pdb'):
        n = plugin + '.' + ext
        files['addons/counterstrikesharp/plugins/' + plugin + '/' + n] = (
            root / project / 'bin/Release/net10.0' / n).read_bytes()
files['RELEASE-NOTES.md'] = (root / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
changed = {n: sha(data) for n, data in files.items() if original[n] != sha(data)}
metadata = json.loads(files['release-components.json'])
metadata['revision'] = '2026-11-12-startup-fixes'
metadata['custom_modules']['BotAI'] = '1.8.15-reaction.2'
metadata['custom_modules']['GG1MapChooser'] = '1.8.0-startup.1'
metadata['startup_fixes'] = {
    'previous_r3_sha256': sha(previous.read_bytes()),
    'changed_files': changed,
    'reaction_hook': 'Validate a nonzero CSS ValveFunction wrapper; never compare its handle to the engine code address.',
    'profile_initialization': 'Resolve the capability after plugin loading; defer entity enumeration to game ticks.',
    'map_menu': 'Read buttons only after pawn and movement services exist.',
    'performance_issue_resolved': False,
    'in_game_validated': False}
files['release-components.json'] = (json.dumps(metadata, indent=2) + '\n').encode()
stage.mkdir()
for n, data in files.items():
    p = stage / n
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(data)
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): new.write(p, p.relative_to(stage).as_posix())
with zipfile.ZipFile(output) as new:
    assert new.testzip() is None and set(new.namelist()) == set(original)
    differences = [n for n, digest in original.items() if sha(new.read(n)) != digest]
    assert set(differences) == set(changed) | {'release-components.json'}
print(json.dumps({'stage': str(stage), 'sha256': sha(output.read_bytes()),
    'unchanged_files': len(original) - len(differences), 'changed_files': differences}, indent=2))
