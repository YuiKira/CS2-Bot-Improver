"""Merge the pinned 1.4.9 Windows release with the existing custom R3."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
upstream, previous, gamedata, output = map(Path, sys.argv[1:])
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(upstream.read_bytes()) == 'ebef45e8e3a3012f5c4bb131bbd6e4cc8351d2c9c0fb059a11c1790048f639f4'
assert sha(previous.read_bytes()) == '09bba6606a6c5624cf23abcfaf69f6dcdc3a4e597bda165ca49c97f73c3cb709'
assert sha(gamedata.read_bytes()) == '6fb5586e3cf39d1b4945fe0a020e698f1e74a887264759e423ffe7cb3ea336f9'
stage = output.parent / (output.stem + '-stage')
assert not stage.exists() and not output.exists()

def vpk_files(data):
    magic, version, size = struct.unpack_from('<III', data)
    assert magic == 0x55AA1234 and version == 2
    offset, result = 28, {}
    def string():
        nonlocal offset
        end = data.index(b'\0', offset)
        value = data[offset:end].decode()
        offset = end + 1
        return value
    while extension := string():
        while directory := string():
            while name := string():
                crc, preload, archive, entry, length, terminator = struct.unpack_from('<IHHIIH', data, offset)
                offset += 18
                assert archive == 0x7FFF and terminator == 0xFFFF
                prefix = '' if directory == ' ' else directory + '/'
                result[prefix + name + '.' + extension] = data[offset:offset + preload] + data[28 + size + entry:28 + size + entry + length]
                offset += preload
    return result

with zipfile.ZipFile(upstream) as official, zipfile.ZipFile(previous) as old:
    files = {n: official.read(n) for n in official.namelist() if not n.endswith('/')}
    official_hashes = {n: sha(data) for n, data in files.items()}
    prefixes = ('BotEquipmentPanel/', 'licenses/', 'runtimes/',
        'addons/counterstrikesharp/plugins/GG1MapChooser/',
        'addons/counterstrikesharp/shared/MapChooserAPI/',
        'addons/counterstrikesharp/plugins/BotRandomizer/')
    names = {'cfg/GGMCmaps.json', 'THIRD_PARTY_NOTICES.md',
        'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll',
        'Microsoft.Web.WebView2.Wpf.dll', 'Newtonsoft.Json.dll', 'WebView2Loader.dll',
        'CS2 Bot Tools.exe', 'CS2 Bot Tools.exe.config',
        'gameinfo.gi', 'backup/Online/gameinfo.gi', 'backup/WithBots/gameinfo.gi'}
    for plugin in ('BotState', 'NadeSystem'):
        names.update('addons/counterstrikesharp/plugins/' + plugin + '/' + plugin + '.' + ext
                     for ext in ('dll', 'deps.json', 'pdb'))
    preserved = {}
    for n in old.namelist():
        if not n.endswith('/') and (n.startswith(prefixes) or n in names):
            files[n] = old.read(n)
            preserved[n] = sha(files[n])
    for mode in ('normal', 'ffa', 'rush'):
        n = 'cfg/my_bot_' + mode + '_config.cfg'
        data = files[n]
        newline = b'\r\n' if b'\r\n' in data else b'\n'
        assert data.count(b'bot_allow_grenades 0') == 1 and b'bot_nades ' not in data
        files[n] = data.replace(b'bot_allow_grenades 0' + newline,
            b'bot_allow_grenades 0' + newline + b'bot_nades normal' + newline)
    vision = json.loads(files['addons/BotVision/config.json'])
    assert vision['bullet_holes']['duration'] == 1.0
    vision['bullet_holes']['duration'] = 2.0
    files['addons/BotVision/config.json'] = (json.dumps(vision, indent=4) + '\n').encode()
    # New retake trees are original; profile reaction values remain byte-identical.
    for n in ('overrides/botprofile.vpk', 'overrides/Low/botprofile.vpk',
              'overrides/Medium/botprofile.vpk', 'overrides/High/botprofile.vpk'):
        current, prior = vpk_files(files[n]), vpk_files(old.read(n))
        assert current['botprofile.db'] == prior['botprofile.db']
        assert 'scripts/ai/retakes/modules/bt_defuse_planted_bomb.kv3' in current
    for ext in ('dll', 'deps.json', 'pdb'):
        n = 'BotAI.' + ext
        files['addons/counterstrikesharp/plugins/BotAI/' + n] = (
            root / 'compat/BotAIReaction/bin/Release/net10.0' / n).read_bytes()
    # Official post-1.0.380 nav-mesh signature correction; no unreviewed native build.
    original_data = json.loads(files['addons/counterstrikesharp/gamedata/gamedata.json'])
    updated_data = json.loads(gamedata.read_bytes())
    key = 'CCSNavArea_IsValidNavMesh'
    assert original_data[key] != updated_data[key]
    assert {k: v for k, v in original_data.items() if k != key} == {k: v for k, v in updated_data.items() if k != key}
    files['addons/counterstrikesharp/gamedata/gamedata.json'] = gamedata.read_bytes()
    files['RELEASE-NOTES.md'] = (root / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
    old_metadata = json.loads(old.read('release-components.json'))
    modules = old_metadata['custom_modules']
    modules['BotAI'] = '1.8.16-reaction.1'
    metadata = {
        'release': 'cs2-v1.4.6-skin-map-r3', 'upstream_version': '1.4.9',
        'revision': '2026-11-14-upstream-149', 'in_game_validated': False,
        'performance_issue_resolved': False,
        'upstream_asset_sha256': sha(upstream.read_bytes()),
        'previous_r3_sha256': sha(previous.read_bytes()),
        'counterstrikesharp': '1.0.380 (official 1.4.9 bundle)',
        'css_gamedata_commit': 'c1b3d1f386933d9cd9d9cb736e126cc93731374e',
        'signature_report_commit': '803171a8047012f80b245c63987bec1bf5f446c6',
        'custom_tuning': old_metadata['custom_tuning'], 'custom_modules': modules,
        'preserved_custom_files': preserved,
        'unchanged_official_files': {n: digest for n, digest in official_hashes.items() if sha(files[n]) == digest},
        'overridden_official_files': {n: sha(files[n]) for n, digest in official_hashes.items() if sha(files[n]) != digest}}
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
    for n, digest in metadata['unchanged_official_files'].items(): assert sha(new.read(n)) == digest
    for n, digest in preserved.items(): assert sha(new.read(n)) == digest
    assert not any(n.endswith('BotRandomizer.custom.json') for n in new.namelist())
print(json.dumps({'stage': str(stage), 'sha256': sha(output.read_bytes()),
    'unchanged_official_files': len(metadata['unchanged_official_files']),
    'preserved_custom_files': len(preserved),
    'overridden_official_files': list(metadata['overridden_official_files'])}, indent=2))
