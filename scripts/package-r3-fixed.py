"""Rebuild existing R3 with runtime reaction delay and HE-only filtering."""
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
source, output = map(Path, sys.argv[1:])
assert source.resolve() != output.resolve() and not output.exists()
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
assert source_hash == '01098fc6b9e9a5c655e782d50771c00465e84104987dc7ea7ece21bda7b9cc98'
baseline_bytes = subprocess.check_output(['git', 'show', 'a5933d8:dist/release-windows.zip'], cwd=root)
changes = {}
with zipfile.ZipFile(io.BytesIO(baseline_bytes)) as baseline:
    for name in ['overrides/Medium/botprofile.vpk', 'overrides/botprofile.vpk']:
        changes[name] = baseline.read(name)
assert hashlib.sha256(changes['overrides/botprofile.vpk']).hexdigest() == 'af84ea2a4b06d3a4ceebe1b7a7b49a924806ccc4d9f06a236fe533277b7302cd'
for plugin, project in [('BotAI', 'BotAIReaction'), ('NadeSystem', 'NadeSystemHEOnly')]:
    for extension in ['dll', 'deps.json', 'pdb']:
        name = plugin + '.' + extension
        changes['addons/counterstrikesharp/plugins/' + plugin + '/' + name] = (root / 'compat' / project / 'bin/Release/net10.0' / name).read_bytes()
changes['RELEASE-NOTES.md'] = (root / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
with zipfile.ZipFile(source) as old:
    metadata = json.loads(old.read('release-components.json'))
    metadata['release'] = 'cs2-v1.4.6-skin-map-r3'
    metadata['revision'] = '2026-10-25-runtime-fix'
    metadata['base_release'] = 'cs2-v1.4.6-skin-map-r2'
    metadata['repack_source_sha256'] = source_hash
    metadata['base_sha256'] = hashlib.sha256(baseline_bytes).hexdigest()
    metadata['in_game_validated'] = False
    metadata['custom_tuning'] = {
        'difficulty': 'Medium', 'implementation': 'native IsNoticable pre-hook',
        'pro_extra_notice_ms': 100, 'rank_extra_notice_ms': 40,
        'profile_reaction_values': 'original baseline (no duplicate increment)',
        'bot_nades': 'normal; HE filtered', 'diagnostic_command': 'bot_reaction_status'}
    metadata['reaction_module_override'] = {
        'version': '1.8.14-reaction.1', 'source': 'compat/BotAIReaction',
        'sha256': hashlib.sha256(changes['addons/counterstrikesharp/plugins/BotAI/BotAI.dll']).hexdigest()}
    metadata['note'] = 'Validated r2 compatibility stack with a runtime notice gate for Medium bots and an HE-only grenade filter. Non-HE grenade behavior, native binaries, skins, maps, aiming and AttackDelay are preserved.'
    for field in ['preserved_file_hashes', 'unchanged_official_files']:
        for path in changes:
            metadata.get(field, {}).pop(path, None)
    changes['release-components.json'] = (json.dumps(metadata, indent=2) + chr(10)).encode()
    assert set(changes).issubset(old.namelist())
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
        for info in old.infolist():
            new.writestr(info, changes.get(info.filename, old.read(info.filename)))
with zipfile.ZipFile(source) as old, zipfile.ZipFile(output) as new:
    assert new.testzip() is None and old.namelist() == new.namelist()
    changed = [path for path in old.namelist() if old.read(path) != new.read(path)]
    assert set(changed) == {path for path, data in changes.items() if old.read(path) != data}
    for mode in ['normal', 'ffa', 'rush']:
        data = new.read('cfg/my_bot_' + mode + '_config.cfg')
        assert b'bot_nades normal' in data and b'bot_allow_grenades 0' in data
    print(json.dumps({'changed': changed, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2))
