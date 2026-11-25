"""Replay slot swaps in the 4x5 bag of InventoryLab's stock showcase (UI Layout Grid + drag and drop, roadmap 16.13) and compare three runs."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = 'ade09cc3-a769-56d7-9832-a2bea14c7e71', '09ca087e-0f44-5886-9546-1553180b2fba'  # Scenes/ShowcaseStock.scene
# "Bag slot 1" … "Bag slot 20" (showcase-src/inventory/stock.py: uuid5 of the scene namespace and "showcase-stock/obj/<name>").
SLOTS = ['d9da877f-ae84-5591-b485-bb188bb3f7c6', 'f9082165-8860-5b07-a57f-65dc611602e0', '9dc3560e-d115-5beb-8d85-79cf971fde06',
         '8173e010-e28f-5445-9776-38e63dc54a57', '6ce3711c-9e47-569e-8057-f459fba29944', 'ab4b4a9f-d875-5a42-b707-5d070001797f',
         '7ed00739-68df-53e0-a42e-b79cb838052f', 'cda0a0b5-e013-549e-b122-8918c680189e', 'daca2f4c-f758-55f9-a4f1-8bb40eab19b1',
         '321ea33b-7ddc-5530-ba6a-1ac0cab76938', 'f24fd96e-31c0-5382-8401-daa972b912e2', '26dd749d-b0e2-5b5b-b879-78cc2d7dc1ec',
         'd50e2ea0-62bf-567d-ae12-46d1d3d285f3', '3ab60b8d-0f09-52b8-a48c-9b1957079af1', '56509c75-1291-5ba8-9216-41bb2ed9a399',
         '9d83dbad-bb2d-5ab8-ad7d-0b65b5faff82', '17d45a5b-64c5-5b5e-9e1b-c42dbd3a4207', '8cc99bbe-08de-5487-ab90-fa251faf48d7',
         'f0016d3f-f972-5a2e-a350-a04cad8f0256', '780a0335-c5f9-58de-8206-f196718e8a9b']
FRAMES = 330
CAPTURES = [15, 30, 50, 80, 115, 175, 205, 245, 320]
MODES = ['scene-play', 'release', 'native-aot']


def center(slot):
    # Grid 4 at (40, 80) (panel 24, 20 + margin 16, 60), no padding, 52 px cells with 8 px gaps: steps of 60.
    return 40 + 60 * (slot % 4) + 26, 80 + 60 * (slot // 4) + 26


def events():
    e = []
    def pointer(frame, slot, down=None, toward=None, t=1.0):
        x, y = center(slot)
        if toward is not None:
            tx, ty = center(toward)
            x, y = x + (tx - x) * t, y + (ty - y) * t
        button = '' if down is None else f', control: "Mouse.Left", down: {str(down).lower()}'
        e.append((frame, f'{{kind: pointer, x: {x}, y: {y}{button}}}'))
    def key(frame, name):
        e.append((frame, f'{{kind: key, control: "{name}", down: true}}'))
        e.append((frame + 1, f'{{kind: key, control: "{name}", down: false}}'))
    def pad(frame, name):
        e.append((frame, f'{{kind: pad, control: "{name}", value: 1}}'))
        e.append((frame + 1, f'{{kind: pad, control: "{name}", value: 0}}'))
    # Starting bag: 0 herb, 1 crystal, 2 key, 4 compass, 6 bread, 7 rope, 9 lantern, 11 map, 12 flask, 15 shield, the rest empty.
    # Mouse drag: herb (slot 0) onto the empty slot 5, 8 move steps.
    pointer(10, 0); pointer(11, 0, down=True)
    for i in range(1, 9): pointer(11 + i, 0, toward=5, t=i / 8)
    pointer(20, 5, down=False)
    # Mouse click to pick the brass key (slot 2), click the empty slot 3 to put it there.
    pointer(40, 2, down=True); pointer(41, 2, down=False)
    pointer(60, 3, down=True); pointer(61, 3, down=False)
    # Keyboard from slot 3: Left, Down to the bread (6), Enter picks, Right, Down to the map (11), Enter swaps them.
    key(90, 'Left'); key(100, 'Down'); key(110, 'Return'); key(120, 'Right'); key(130, 'Down'); key(140, 'Return')
    # Pad: Left to 10, South picks it, East cancels; Down, Right to the shield (15), South picks, Up, Up to the rope (7),
    # South swaps them.
    pad(190, 'Pad.DpadLeft'); pad(200, 'Pad.South'); pad(210, 'Pad.East'); pad(220, 'Pad.DpadDown'); pad(230, 'Pad.DpadRight')
    pad(240, 'Pad.South'); pad(250, 'Pad.DpadUp'); pad(260, 'Pad.DpadUp'); pad(270, 'Pad.South')
    return sorted(e, key=lambda item: item[0])


def recording(path):
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {i}\n' for f, i in events())
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:\n' + lines, encoding='utf-8')


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    settings = dest / 'GameSettings'
    settings.write_text(re.sub(r'(?m)^startScene: .*$', f'startScene: {START}', settings.read_text(encoding='utf-8'), count=1), encoding='utf-8')
    shutil.copyfile(HERE / 'TraceBag.source', dest / 'Scripts/TraceBag.cs')
    scene = dest / 'Scenes/ShowcaseStock.scene'
    slots = ', '.join(f'"{s}"' for s in SLOTS)
    text = scene.read_text(encoding='utf-8')
    assert text.count('\ndefaultCameraId:') == 1
    trace = ('- id: 5b1c7f2e-2d4a-4e0b-8c3d-16d313000001\n  name: "Bag trace"\n  x: 0\n  y: 0\n'
             f'  width: 1\n  height: 1\n  components:\n  - type: script\n    script: TraceBag\n    values: {{"Slots": [{slots}]}}')
    scene.write_text(text.replace('\ndefaultCameraId:', '\n' + trace + '\ndefaultCameraId:', 1), encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'bag.inputreplay')
    env = dict(os.environ, MONOON_BAG_TRACE=str(out / 'bag.trace'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'bag.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(out)


def check(out):
    rows = [line.split('\t') for line in (out / 'bag.trace').read_text(encoding='utf-8').splitlines()]
    last = {}
    for frame, focus, carried, slots in rows:
        last[int(frame)] = (focus, carried, slots.split('|'))
    at = lambda f: last[max(k for k in last if k <= f)]
    # A slot's text is its icon tag; a picked-up item leaves its slot (BagSlots carries it on a ghost) until it lands.
    names = lambda f: [m.group(1) if (m := re.search(r'<icon=(\w+)>', t)) else '' for t in at(f)[2]]
    assert names(5)[0] == 'herb' and names(5)[5] == '', names(5)
    assert names(15)[0] == '' and at(15)[1] == '0', 'a mouse drag lifts the herb out of its slot'
    assert names(40)[0] == '' and names(40)[5] == 'herb', 'mouse drag moves the herb to slot 5'
    assert at(50)[1] == '2' and names(50)[2] == '', 'click picks the key up'
    assert names(80)[2] == '' and names(80)[3] == 'key' and at(80)[1] == '-', 'click on slot 3 puts the key there'
    assert at(115)[:2] == ('6', '6') and names(115)[6] == '', 'keyboard picks the bread'
    assert names(175)[6] == 'map' and names(175)[11] == 'bread' and at(175)[1] == '-', 'Enter on the map swaps them'
    assert at(205)[:2] == ('10', '10') and at(215)[1] == '-' and names(215) == names(175), 'pad East cancels without a drop'
    assert at(245)[:2] == ('15', '15') and names(320)[7] == 'shield' and names(320)[15] == 'rope' and at(320)[:2] == ('7', '-'), \
        'pad drop swaps the shield and the rope'
    for frame in CAPTURES:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    result = dict(changes=len(rows), final=names(FRAMES))
    (out / 'result.json').write_text(json.dumps(result, ensure_ascii=False) + '\n', encoding='utf-8')
    print(result, flush=True)


def compare(root):
    traces = {mode: (root / mode / 'bag.trace').read_bytes() for mode in MODES}
    assert len(set(traces.values())) == 1, 'bag traces differ between runs'
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    lines = ['frame\t' + '\t'.join(MODES) + '\tidentical']
    for frame in CAPTURES:
        hashes = [digest(root / mode / 'captures' / f'{frame:04}.png') for mode in MODES]
        assert len(set(hashes)) == 1, f'captures differ at frame {frame}: {hashes}'
        lines.append(f'{frame}\t' + '\t'.join(h[:12] for h in hashes) + '\tTrue')
    (root / 'comparison.tsv').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'check': check, 'compare': compare}[command](*(Path(p) for p in paths))
