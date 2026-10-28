"""Rewrites the warp menu (M) of the Season 6 1.04 client for the map level ladder of the server.

- Orders the maps by the ladder (Lorencia, Noria, Elbeland, Devias, ..., Swamp of Calmness and Crywolf, Raklion,
  then Vulcanus and Arena).
- Adds the entries which the server has but the client files don't (Crywolf). The files have room for 50 entries,
  and the client reads as many as the count at the start of the file says.
- Sets the minimum levels of the maps which changed on the server.
- Replaces the zen cost by the danger level of the map (the server doesn't charge zen for warps anymore),
  and renames the "Cost" column to "Danger level".

The index and the gate of each existing entry are never changed, because the client sends the index to the server.
Added entries use the index and gate of the warp list of the server (see VersionSeasonSix/Gates.cs).

Usage: python warpmenu.py <client folder>
The original files are kept next to the changed ones as *.bak, and the script always reads the originals,
so it can be run again after changing the tables below.
"""

import os
import shutil
import struct
import sys

KEY = b'\xfc\xcf\xab'
RECORD_SIZE = 84
TEXT_COST_INDEX = 936
TEXT_COST = 'Danger level'

# The order of the entries by their index: Lorencia, Noria, Elbeland 1-3, Devias 1-4, Dungeon 1-3, Lost Tower 1-7,
# Atlans 1-3, Tarkan 1-2, Aida 1-2, Icarus, Kanturu Ruins 1-3, Kanturu Relics, Karutan 1-2,
# Swamp of Calmness and Crywolf (in parallel), Raklion, then Vulcanus, which isn't on the ladder, and Arena.
# Unknown entries keep their relative order after these.
ORDER = [2, 3, 31, 32, 43, 4, 5, 6, 7, 8, 9, 10, 14, 15, 16, 17, 18, 19, 20, 11, 12, 13, 21, 22, 25, 27, 23,
         28, 29, 45, 30, 46, 47, 33, 26, 34, 42, 1]

# The entries which are added when the file doesn't have them: index -> (name, map name, level, gate).
# The level is the one of the server, the gate is the Crywolf spawn gate of the server.
ADDED = {26: ('Crywolf', 'Crywolf_Fortress', 270, 118)}
MAX_LEVEL = 400

# The minimum levels which changed on the server, by index.
LEVELS = {28: 180, 29: 180, 45: 180, 46: 240, 47: 250, 33: 270, 26: 270, 34: 280}

# The danger level, by index. Entries which aren't listed (e.g. Arena and Vulcanus, which isn't on the ladder) get 0.
DANGER = {}
for danger, indexes in enumerate([
        [2, 3, 31, 32, 43],  # Lorencia, Noria, Elbeland
        [4, 5, 6, 7],  # Devias
        [8, 9, 10],  # Dungeon
        [14, 15, 16, 17, 18, 19, 20],  # Lost Tower
        [11, 12, 13],  # Atlans
        [21, 22],  # Tarkan
        [25, 27],  # Aida
        [23],  # Icarus
        [28, 29, 45],  # Kanturu Ruins
        [30],  # Kanturu Relics
        [46, 47],  # Karutan
        [33, 26],  # Swamp of Calmness and Crywolf
        [34],  # Raklion
], start=1):
    for index in indexes:
        DANGER[index] = danger


def xor(data):
    return bytes(b ^ KEY[i % 3] for i, b in enumerate(data))


def original(path):
    backup = path + '.bak'
    if not os.path.exists(backup):
        shutil.copy2(path, backup)
    with open(backup, 'rb') as f:
        return f.read()


def convert_movereq(path):
    data = original(path)
    count = struct.unpack_from('<i', data, 0)[0]
    body = data[4:]
    records = [bytearray(xor(body[i * RECORD_SIZE:(i + 1) * RECORD_SIZE])) for i in range(count)]
    rest = body[count * RECORD_SIZE:]

    existing = {struct.unpack_from('<i', record, 0)[0] for record in records}
    for index, (name, map_name, level, gate) in ADDED.items():
        if index in existing:
            continue
        if len(rest) < RECORD_SIZE:
            sys.exit(f'{path}: no room to add entry {index}')
        records.append(bytearray(struct.pack('<i32s32siiii', index, name.encode('latin1'), map_name.encode('latin1'),
                                             level, MAX_LEVEL, 0, gate)))
        rest = rest[RECORD_SIZE:]
        count += 1

    def sort_key(item):
        position, record = item
        index = struct.unpack_from('<i', record, 0)[0]
        return (ORDER.index(index), 0) if index in ORDER else (len(ORDER) - 1, position - len(records))

    ordered = [record for _, record in sorted(enumerate(records), key=sort_key)]
    for record in ordered:
        index = struct.unpack_from('<i', record, 0)[0]
        if index in LEVELS:
            struct.pack_into('<i', record, 68, LEVELS[index])
        struct.pack_into('<i', record, 76, DANGER.get(index, 0))

    with open(path, 'wb') as f:
        f.write(struct.pack('<i', count) + b''.join(xor(bytes(r)) for r in ordered) + rest)
    print(f'{path}: {count} entries')


def convert_text(path):
    data = original(path)
    count = struct.unpack_from('<I', data, 2)[0]
    out = bytearray(data[:6])
    position = 6
    for _ in range(count):
        index, length = struct.unpack_from('<ii', data, position)
        text = data[position + 8:position + 8 + length]
        position += 8 + length
        if index == TEXT_COST_INDEX:
            text = xor(TEXT_COST.encode('latin1'))
        out += struct.pack('<ii', index, len(text)) + text

    out += data[position:]
    with open(path, 'wb') as f:
        f.write(out)
    print(f'{path}: entry {TEXT_COST_INDEX} = {TEXT_COST!r}')


def main():
    if len(sys.argv) != 2:
        sys.exit('Usage: python warpmenu.py "<client folder>" (quote the path if it has spaces)')

    local = os.path.join(sys.argv[1], 'Data', 'Local')
    if not os.path.isdir(local):
        sys.exit(f'Not a client folder, {local} does not exist. Quote the path if it has spaces.')

    converted = 0
    for name in ['Movereq.bmd', 'Eng/movereq_eng.bmd', 'Por/movereq_por.bmd', 'Spn/movereq_spn.bmd']:
        path = os.path.join(local, name)
        if os.path.exists(path):
            convert_movereq(path)
            converted += 1

    text = os.path.join(local, 'Eng', 'Text_eng.bmd')
    if os.path.exists(text):
        convert_text(text)
        converted += 1

    if converted == 0:
        sys.exit(f'No movereq or Text_eng.bmd file found in {local}.')


if __name__ == '__main__':
    main()
