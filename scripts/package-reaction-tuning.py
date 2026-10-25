"""Apply the requested Medium reaction tuning to the validated r2 package."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import zlib
import zipfile

ROOT = Path(__file__).resolve().parent.parent
TAG = 'cs2-v1.4.6-skin-map-r3'


def read_vpk(blob):
    signature, version, tree_size = struct.unpack_from('<III', blob)
    assert signature == 0x55AA1234 and version in (1, 2)
    header = 12 if version == 1 else 28
    pos = header
    entries = []

    def string():
        nonlocal pos
        end = blob.index(0, pos)
        value = blob[pos:end].decode()
        pos = end + 1
        return value

    while extension := string():
        while directory := string():
            while name := string():
                crc, preload, archive, offset, length, terminator = struct.unpack_from('<IHHIIH', blob, pos)
                pos += 18
                assert archive == 0x7FFF and terminator == 0xFFFF
                data = blob[pos:pos + preload]
                pos += preload
                start = header + tree_size + offset
                data += blob[start:start + length]
                assert zlib.crc32(data) == crc
                entries.append((extension, directory, name, data))
    return entries


def write_vpk(entries):
    tree = bytearray()
    payload = bytearray()
    for extension in dict.fromkeys(e[0] for e in entries):
        tree.extend(extension.encode() + bytes([0]))
        for directory in dict.fromkeys(e[1] for e in entries if e[0] == extension):
            tree.extend(directory.encode() + bytes([0]))
            for ext, folder, name, data in entries:
                if (ext, folder) != (extension, directory):
                    continue
                tree.extend(name.encode() + bytes([0]))
                tree.extend(struct.pack('<IHHIIH', zlib.crc32(data), 0, 0x7FFF, len(payload), len(data), 0xFFFF))
                payload.extend(data)
            tree.append(0)
        tree.append(0)
    tree.append(0)
    return struct.pack('<III', 0x55AA1234, 1, len(tree)) + tree + payload


def main():
    source, output = map(Path, sys.argv[1:])
    assert source.resolve() != output.resolve() and not output.exists()
    old_db = subprocess.check_output(['git', 'show', 'a5933d8:overrides/Medium/botprofile.db'], cwd=ROOT)
    new_db = (ROOT / 'overrides/Medium/botprofile.db').read_bytes()
    with zipfile.ZipFile(source) as old:
        original = old.read('overrides/Medium/botprofile.vpk')
        assert old.read('overrides/botprofile.vpk') == original
        entries = read_vpk(original)
        db_entries = [e for e in entries if e[:3] == ('db', ' ', 'botprofile')]
        assert len(db_entries) == 1 and db_entries[0][3] == old_db, 'Source profile must match the released VPK'
        updated = [(ext, folder, name, new_db if (ext, folder, name) == ('db', ' ', 'botprofile') else data)
                   for ext, folder, name, data in entries]
        packed = write_vpk(updated)
        assert read_vpk(packed) == updated
        changes = {'overrides/Medium/botprofile.vpk': packed, 'overrides/botprofile.vpk': packed}
        for item in old.namelist():
            if item.startswith('cfg/my_bot_') and item.endswith('.cfg'):
                data = old.read(item)
                newline = bytes([13, 10]) if bytes([13, 10]) in data else bytes([10])
                if b'bot_nades ' not in data:
                    assert b'bot_allow_grenades 0' in data, item
                    data = data.replace(b'bot_allow_grenades 0', b'bot_allow_grenades 0' + newline + b'bot_nades off', 1)
                (ROOT / item).write_bytes(data)
                changes[item] = data
        metadata = json.loads(old.read('release-components.json'))
        metadata['release'] = TAG
        metadata['revision_date'] = '2026-10-25'
        metadata['base_release'] = 'cs2-v1.4.6-skin-map-r2'
        metadata['base_sha256'] = hashlib.sha256(source.read_bytes()).hexdigest()
        metadata['in_game_validated'] = False
        metadata['custom_tuning'] = {
            'difficulty': 'Medium', 'pro_reaction_delta_seconds': 0.1,
            'rank_reaction_delta_seconds': 0.04, 'bot_nades': 'off',
            'note': 'Aim, attack delay, native binaries, skins and maps are unchanged. Native grenade usage remains disabled.'}
        for field in ['preserved_file_hashes', 'unchanged_official_files']:
            hashes = metadata.get(field, {})
            for item in changes:
                hashes.pop(item, None)
        changes['release-components.json'] = (json.dumps(metadata, indent=2) + chr(10)).encode()
        changes['RELEASE-NOTES.md'] = (ROOT / 'dist/REACTION-TUNING-NOTES.md').read_bytes()
        with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as new:
            for info in old.infolist():
                new.writestr(info, changes.get(info.filename, old.read(info.filename)))
    with zipfile.ZipFile(source) as old, zipfile.ZipFile(output) as new:
        assert old.namelist() == new.namelist()
        assert new.testzip() is None
        changed = [name for name in old.namelist() if old.read(name) != new.read(name)]
        assert set(changed) == set(changes)
        print(json.dumps({'changed': changed, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2))


if __name__ == '__main__':
    main()
