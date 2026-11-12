"""Refresh Valve's gameinfo baseline without changing plugin binaries or tuning."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
previous, valve, output = map(Path, sys.argv[1:4])
dotnet = sys.argv[4]
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(previous.read_bytes()) == '4362c003da86d5d8948abd90206ee16ef13385dca4ba8f9a67c11cef41d68c92'
assert sha(valve.read_bytes()) == 'c6ce40611cc8816dd15c87ccdfb3bdc9d33bf9d6ff010a5e7afe66831e135a29'
baseline = root / 'dist/gameinfo-online.gi'
assert baseline.read_text() == valve.read_text()  # Preserve the repository's CRLF format.
stage = output.parent / (output.stem + '-stage')
assert not stage.exists() and not output.exists()
with zipfile.ZipFile(previous) as old:
    files = {n: old.read(n) for n in old.namelist() if not n.endswith('/')}
original = {n: sha(data) for n, data in files.items()}
stage.mkdir()
for n, data in files.items():
    p = stage / n
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(data)
subprocess.run([dotnet, 'run', '--project', str(root / 'scripts/GameInfoTool/GameInfoTool.csproj'),
    '-c', 'Release', '--', str(baseline), str(stage)], check=True)
gameinfos = ('gameinfo.gi', 'backup/Online/gameinfo.gi', 'backup/WithBots/gameinfo.gi')
assert (stage / gameinfos[0]).read_bytes() == (stage / gameinfos[2]).read_bytes()
assert (stage / gameinfos[1]).read_text() == valve.read_text()
for n in gameinfos:
    text = (stage / n).read_text()
    assert 'ForceUseBuildVersion\t1' in text
    assert '"cl_interp_parallel"\t\t\t"1"' in text
    assert '"sv_recvbuf_messages"' not in text
    assert '"net_limit_sv_recvbuffer_kb" "128"' in text
    assert text.count('Game\tcsgo/addons/metamod') == (0 if 'Online' in n else 1)
notes = root / 'dist/REACTION-TUNING-NOTES.md'
(stage / 'RELEASE-NOTES.md').write_bytes(notes.read_bytes())
metadata = json.loads((stage / 'release-components.json').read_text())
metadata['revision'] = '2026-11-12-gameinfo-sync'
metadata['gameinfo_update'] = {
    'source_repo': 'SteamTracking/GameTracking-CS2',
    'source_commit': '82e2b9d62cc97f1358c68a220a4a392c9e41c509',
    'source_path': 'game/csgo/gameinfo.gi', 'source_sha256': sha(valve.read_bytes()),
    'previous_r3_sha256': sha(previous.read_bytes()),
    'changes': ['ForceUseBuildVersion=1', 'new network receive limits', 'cl_interp_parallel=1'],
    'in_game_validated': False, 'performance_issue_resolved': False}
(stage / 'release-components.json').write_text(json.dumps(metadata, indent=2) + '\n')
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): new.write(p, p.relative_to(stage).as_posix())
with zipfile.ZipFile(output) as new:
    assert new.testzip() is None and set(new.namelist()) == set(original)
    changed = [n for n, digest in original.items() if sha(new.read(n)) != digest]
    assert set(changed) == set(gameinfos) | {'RELEASE-NOTES.md', 'release-components.json'}
print(json.dumps({'stage': str(stage), 'sha256': sha(output.read_bytes()),
    'unchanged_files': len(original) - len(changed), 'changed_files': changed}, indent=2))
