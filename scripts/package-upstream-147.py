"""Merge pinned official 1.4.7 and custom R3 without replacing user settings."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
upstream, previous, output = map(Path, sys.argv[1:])
assert not output.exists() and output.resolve() not in (upstream.resolve(), previous.resolve())
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(upstream.read_bytes()) == '2018c8925dc5fced00a5dbde87efa65de4181613f2e0246f4a416349eae36b56'
assert sha(previous.read_bytes()) == '5bb88391b873d7c3a522039f6c7a368af30bd4250a4de1cef5bf9e0a8aca55d4'
stage = output.parent / (output.stem + '-stage')
assert not stage.exists()
with zipfile.ZipFile(upstream) as official, zipfile.ZipFile(previous) as old:
    files = {n: official.read(n) for n in official.namelist() if not n.endswith('/')}
    official_hashes = {n: sha(data) for n, data in files.items()}
    # Retain custom skin/map UI and map vote module; never import old native stacks.
    prefixes = ('BotEquipmentPanel/', 'licenses/', 'runtimes/',
                'addons/counterstrikesharp/plugins/GG1MapChooser/',
                'addons/counterstrikesharp/shared/MapChooserAPI/')
    names = {'cfg/GGMCmaps.json', 'THIRD_PARTY_NOTICES.md',
             'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll',
             'Microsoft.Web.WebView2.Wpf.dll', 'Newtonsoft.Json.dll', 'WebView2Loader.dll'}
    preserved = {}
    for n in old.namelist():
        if not n.endswith('/') and (n.startswith(prefixes) or n in names):
            files[n] = old.read(n)
            preserved[n] = sha(files[n])
    # Three mode cfg files differ only by our explicit non-HE normal-mode command.
    for mode in ('normal', 'ffa', 'rush'):
        n = 'cfg/my_bot_' + mode + '_config.cfg'
        data = files[n]
        newline = b'\r\n' if b'\r\n' in data else b'\n'
        assert data.count(b'bot_allow_grenades 0') == 1 and b'bot_nades ' not in data
        files[n] = data.replace(b'bot_allow_grenades 0' + newline,
                                b'bot_allow_grenades 0' + newline + b'bot_nades normal' + newline)
    # Difficulty profiles remain original and unmodified; no double reaction increment.
    for n in ('overrides/Low/botprofile.vpk', 'overrides/Medium/botprofile.vpk',
              'overrides/High/botprofile.vpk', 'overrides/botprofile.vpk'):
        assert files[n] == old.read(n), 'Unexpected profile change: ' + n
    vision = json.loads(files['addons/BotVision/config.json'])
    assert vision['bullet_holes']['duration'] == 1.0
    vision['bullet_holes']['duration'] = 2.0
    files['addons/BotVision/config.json'] = (json.dumps(vision, indent=4) + '\n').encode()
    for plugin, project in [('BotAI', 'compat/BotAIReaction'),
                            ('NadeSystem', 'compat/NadeSystemHEOnly'),
                            ('BotState', 'compat/BotStateNoHurtReveal'),
                            ('BotRandomizer', 'addons/counterstrikesharp/plugins/BotRandomizer')]:
        for ext in ('dll', 'deps.json', 'pdb'):
            n = plugin + '.' + ext
            files['addons/counterstrikesharp/plugins/' + plugin + '/' + n] = (root / project / 'bin/Release/net10.0' / n).read_bytes()
    for n in ('CS2 Bot Tools.exe', 'CS2 Bot Tools.exe.config'):
        files[n] = (root / 'DesktopPanel/bin/Release/net48' / n).read_bytes()
    files['RELEASE-NOTES.md'] = (root / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
    stage.mkdir()
    for n, data in files.items():
        p = stage / n
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
    metadata = {
        'release': 'cs2-v1.4.6-skin-map-r3', 'upstream_version': '1.4.7',
        'revision': '2026-10-26-upstream-147', 'in_game_validated': False,
        'upstream_asset_sha256': sha(upstream.read_bytes()),
        'previous_r3_sha256': sha(previous.read_bytes()),
        'custom_tuning': {'difficulty': 'all (Low, Medium, High)',
            'pro_extra_notice_ms': 100, 'rank_extra_notice_ms': 40,
            'damage_triggered_smoke_reveal': False, 'bullet_hole_duration_seconds': 2.0,
            'bot_nades': 'normal; HE filtered', 'diagnostic_command': 'bot_reaction_status'},
        'custom_modules': {'BotAI': '1.8.14-reaction.2', 'NadeSystem': '1.2.3-nohe.1',
            'BotState': '1.10.5-nohurtreveal.1', 'BotRandomizer': '1.6.2-upstream147.1'},
        'preserved_custom_files': preserved,
        'unchanged_official_files': {n: digest for n, digest in official_hashes.items() if sha(files[n]) == digest},
        'overridden_official_files': {n: sha(files[n]) for n, digest in official_hashes.items() if sha(files[n]) != digest}}
    (stage / 'release-components.json').write_text(json.dumps(metadata, indent=2) + '\n')
    subprocess.run(['node', str(root / 'scripts/generate-uninstall-manifest.js'), str(stage)], check=True)
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
        for p in sorted(stage.rglob('*')):
            if p.is_file(): new.write(p, p.relative_to(stage).as_posix())
with zipfile.ZipFile(output) as new:
    assert new.testzip() is None
    for n, digest in metadata['unchanged_official_files'].items(): assert sha(new.read(n)) == digest
    for n, digest in preserved.items(): assert sha(new.read(n)) == digest
    assert 'Panel v1.4.7.exe' in new.namelist() and 'Panel v1.4.6.exe' not in new.namelist()
    assert not any(n.endswith('BotRandomizer.custom.json') for n in new.namelist())
print(json.dumps({'stage': str(stage), 'sha256': sha(output.read_bytes()),
    'unchanged_official_count': len(metadata['unchanged_official_files']),
    'preserved_custom_count': len(preserved),
    'overridden_official_files': list(metadata['overridden_official_files'])}, indent=2))
