"""Replay pad input in InventoryLab's 1,000-item catalog and check which item IDs were used."""
import json
import math
import os
from pathlib import Path
import subprocess
import sys

PROJECT = 'ade09cc3-a769-56d7-9832-a2bea14c7e71'
START = '6a7b735e-336d-5674-b2e7-a136db5a7771'  # Inventory.scene; C opens Catalog.scene
DOWN_BEFORE_SORT, DOWN_AFTER_SORT = 41, 3
# Independent copy of the generated catalog in Scripts/Catalog/CatalogScreen.cs.
ADJECTIVES = ['낡은', '빛나는', '작은', '단단한', '향긋한', '차가운', '무거운', '가벼운']
NOUNS = ['약초', '수정', '열쇠', '밧줄', '등불', '지도', '물병', '붕대', '화살', '방패']
ITEMS = [(f'item-{i:04}', f'{ADJECTIVES[i * 5 % 8]} {NOUNS[i * 3 % 10]}') for i in range(1, 1001)]
# Viewport 346, row 34 + gap 4: rows covering the viewport plus one partly visible row.
MAX_ROWS = math.ceil(346 / 38) + 1


def recording(path):
    events, frame = [], 2
    def add(tick, kind, control, state):
        value = f'down: {state}' if kind == 'key' else f'value: {state}'
        events.append(f'- frame: {tick}\n  timeTicks: {tick * 166667}\n  input: {{kind: {kind}, control: "{control}", {value}}}\n')
    def press(kind, control, gap=4):
        nonlocal frame
        add(frame, kind, control, 'true' if kind == 'key' else 1)
        add(frame + 1, kind, control, 'false' if kind == 'key' else 0)
        frame += gap
    press('key', 'C', 20)  # load the catalog scene
    for _ in range(DOWN_BEFORE_SORT): press('pad', 'Pad.DpadDown')
    press('pad', 'Pad.North', 8)  # Sort: ID order -> name order
    press('pad', 'Pad.South', 8)  # use the selected row
    for _ in range(DOWN_AFTER_SORT): press('pad', 'Pad.DpadDown')
    press('pad', 'Pad.South', 8)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' +
                    ', '.join(['166667'] * (frame + 10)) + ']\nevents:\n' + ''.join(events), encoding='utf-8')


def check(out):
    values = {}
    for line in (out / 'profile' / 'slot-002.save').read_text(encoding='utf-8').splitlines():
        if line.startswith('  ') and ':' in line:
            key, value = line.strip().split(':', 1)
            values[key] = value.strip().strip('"')
    used = values['used'].split(',')
    selected = ITEMS[DOWN_BEFORE_SORT][0]
    by_name = sorted(ITEMS, key=lambda item: (item[1].encode('utf-16-be'), item[0]))
    moved = [item[0] for item in by_name].index(selected)
    expected = [selected, by_name[moved + DOWN_AFTER_SORT][0]]
    rows = int(float(values['rows']))
    assert moved != DOWN_BEFORE_SORT, 'sorting must move the selected item'
    assert used == expected, (used, expected)
    assert values['order'] == '이름순' and int(float(values['items'])) == 1000, values
    assert rows <= MAX_ROWS, (rows, MAX_ROWS)
    result = {'selectedBeforeSort': selected, 'indexAfterSort': moved, 'used': used, 'rowObjects': rows, 'items': 1000}
    (out / 'result.json').write_text(json.dumps(result, ensure_ascii=False) + '\n', encoding='utf-8')
    print(result, flush=True)


def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Catalog.inputreplay')
    env = dict(os.environ, MONOON_USER_DATA=str(out / 'profile'),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    subprocess.run([str(player.resolve()), '--replay-input', str(out / 'Catalog.inputreplay'), str(out / 'profile')],
                   env=env, check=True)
    check(out)


if __name__ == '__main__':
    {'play': play, 'check': check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
