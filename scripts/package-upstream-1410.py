"""Merge upstream 1.4.10 with the customized, current R3 package."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parent.parent
upstream, previous, output = map(Path, sys.argv[1:])
sha = lambda data: hashlib.sha256(data).hexdigest()
assert sha(upstream.read_bytes()) == '8f6013be680236e47ff984e85ca9f6d06224e9f6cde2a293df7296639028b515'
assert sha(previous.read_bytes()) == '0e37343a79a9cf7dc0ed0b5aad7a665ec67504baa6c45771ee88c05b28448178'
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
                _, preload, archive, entry, length, terminator = struct.unpack_from('<IHHIIH', data, offset)
                offset += 18
                assert archive == 0x7FFF and terminator == 0xFFFF
                prefix = '' if directory == ' ' else directory + '/'
                result[prefix + name + '.' + extension] = (
                    data[offset:offset + preload] + data[28 + size + entry:28 + size + entry + length])
                offset += preload
    return result

with zipfile.ZipFile(upstream) as official, zipfile.ZipFile(previous) as old:
    files = {n: official.read(n) for n in official.namelist() if not n.endswith('/')}
    official_hashes = {n: sha(data) for n, data in files.items()}
    # Preserve the custom cosmetics/map UI, map voting, original launcher, and modules
    # that carry user-requested tuning. Use the new official cosmetic catalog below.
    prefixes = ('BotEquipmentPanel/', 'licenses/', 'runtimes/',
        'addons/counterstrikesharp/plugins/GG1MapChooser/',
        'addons/counterstrikesharp/shared/MapChooserAPI/',
        'addons/counterstrikesharp/plugins/BotRandomizer/')
    names = {'cfg/GGMCmaps.json', 'THIRD_PARTY_NOTICES.md',
        'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll',
        'Microsoft.Web.WebView2.Wpf.dll', 'Newtonsoft.Json.dll', 'WebView2Loader.dll',
        'CS2 Bot Tools.exe', 'CS2 Bot Tools.exe.config',
        'gameinfo.gi', 'backup/Online/gameinfo.gi', 'backup/WithBots/gameinfo.gi',
        'addons/BotVision/config.json'}
    for plugin in ('BotState', 'NadeSystem'):
        names.update('addons/counterstrikesharp/plugins/' + plugin + '/' + plugin + '.' + ext
                     for ext in ('dll', 'deps.json', 'pdb'))
    preserved = {}
    for n in old.namelist():
        if n.endswith('/'):
            continue
        if n.startswith(prefixes) or n in names:
            if n == 'addons/counterstrikesharp/plugins/BotRandomizer/cosmetic_catalog.json':
                continue
            files[n] = old.read(n)
            preserved[n] = sha(files[n])
    # Updated upstream catalog adds 773 stickers and 8 music kits.
    catalog = 'addons/counterstrikesharp/plugins/BotRandomizer/cosmetic_catalog.json'
    assert catalog in files
    assert len(json.loads(files[catalog])['musicKits']) == 108
    previous_catalog = json.loads(old.read(catalog))
    assert len(previous_catalog['musicKits']) == 100
    assert len(json.loads(files[catalog])['stickerKits']) == len(previous_catalog['stickerKits']) + 773
    # Keep the embedded equipment UI searchable for every new sticker ID too.
    panel_stickers_path = 'BotEquipmentPanel/sticker-catalog.json'
    panel_stickers = json.loads(files[panel_stickers_path])
    known = {int(item['id']) for item in panel_stickers['stickers']}
    updated_catalog = json.loads(files[catalog])
    for sticker in updated_catalog['stickerKits']:
        sticker_id = int(sticker['defIndex'])
        if sticker_id in known:
            continue
        finish = str(sticker.get('finish') or 'paper')
        panel_stickers['stickers'].append({
            'id': sticker_id, 'name': f'印花 #{sticker_id}',
            'english': f'Sticker #{sticker_id}', 'image': None,
            'finish': finish, 'type': 'Other'})
        known.add(sticker_id)
    panel_stickers['stickers'].sort(key=lambda item: int(item['id']))
    assert len(panel_stickers['stickers']) >= len(updated_catalog['stickerKits'])
    files[panel_stickers_path] = (json.dumps(panel_stickers, ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    preserved.pop(panel_stickers_path, None)
    merged_custom_files = {panel_stickers_path: sha(files[panel_stickers_path])}
    # Keep original behavior for every grenade type except the user's HE-only block.
    for mode in ('normal', 'ffa', 'rush'):
        n = 'cfg/my_bot_' + mode + '_config.cfg'
        data = files[n]
        newline = b'\r\n' if b'\r\n' in data else b'\n'
        assert data.count(b'bot_allow_grenades 0') == 1
        if b'bot_nades normal' not in data:
            files[n] = data.replace(b'bot_allow_grenades 0' + newline,
                b'bot_allow_grenades 0' + newline + b'bot_nades normal' + newline)
    vision_path = 'addons/BotVision/config.json'
    vision = json.loads(files[vision_path])
    assert vision['bullet_holes']['duration'] == 2.0
    # Current official native plugin behavior with the user's 2 second tunnel.
    vision['bullet_holes']['duration'] = 2.0
    files[vision_path] = (json.dumps(vision, indent=4) + '\n').encode()
    # Difficulty profiles retain original ReactionTime, while carrying official retake trees.
    for n in ('overrides/botprofile.vpk', 'overrides/Low/botprofile.vpk',
              'overrides/Medium/botprofile.vpk', 'overrides/High/botprofile.vpk'):
        current, prior = vpk_files(files[n]), vpk_files(old.read(n))
        assert current['botprofile.db'] == prior['botprofile.db']
        assert 'scripts/ai/retakes/modules/bt_defuse_planted_bomb.kv3' in current
    # Rebuild the custom reaction hook on the upstream 1.9.1 patch-loader and API 1.0.381.
    project = root / 'compat/BotAIReaction/bin/Release/net10.0'
    for ext in ('dll', 'deps.json', 'pdb', 'gamedata.json'):
        n = 'BotAI.' + ext if ext != 'gamedata.json' else ext
        target_name = 'addons/counterstrikesharp/plugins/BotAI/' + n
        source_name = 'gamedata.json' if ext == 'gamedata.json' else n
        files[target_name] = (project / source_name).read_bytes()
    modules = {
        'BotAI': '1.9.1-reaction.1', 'NadeSystem': '1.2.3-nohe.1',
        'BotState': '1.10.6-nohurtreveal.1', 'BotRandomizer': '1.6.2-upstream148.1',
        'GG1MapChooser': '1.8.0-startup.1'}
    files['RELEASE-NOTES.md'] = (root / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
    metadata = {
        'release': 'cs2-v1.4.6-skin-map-r3', 'upstream_version': '1.4.10',
        'revision': '2026-11-25-upstream-1410', 'in_game_validated': False,
        'performance_issue_resolved': False,
        'upstream_asset_sha256': sha(upstream.read_bytes()),
        'previous_r3_sha256': sha(previous.read_bytes()),
        'counterstrikesharp': '1.0.381 (upstream bundle)',
        'botai_base': 'v1.9.1 (commit 9263804c6661ab433ed019e28999e8c3c5f40418)',
        'custom_tuning': json.loads(old.read('release-components.json'))['custom_tuning'],
        'custom_modules': modules,
        'preserved_custom_files': preserved,
        'merged_custom_files': merged_custom_files,
        'unchanged_official_files': {n: digest for n, digest in official_hashes.items()
                                     if n in files and sha(files[n]) == digest},
        'overridden_official_files': {n: sha(files[n]) for n, digest in official_hashes.items()
                                     if n in files and sha(files[n]) != digest}}
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
    assert new.read('addons/counterstrikesharp/plugins/BotAI/gamedata.json') == (
        root / 'compat/BotAIReaction/gamedata.json').read_bytes()
    assert 'Panel.exe' in new.namelist() and 'Panel v1.4.7.exe' not in new.namelist()
    assert not any(n.endswith('BotRandomizer.custom.json') for n in new.namelist())
print(json.dumps({'stage': str(stage), 'sha256': sha(output.read_bytes()),
    'unchanged_official_files': len(metadata['unchanged_official_files']),
    'preserved_custom_files': len(preserved),
    'overridden_official_files': list(metadata['overridden_official_files'])}, indent=2))
