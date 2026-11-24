"""Replay slot swaps in InventoryLab's 4x5 bag (UI Layout Grid + drag and drop, roadmap 16.13) and compare three runs."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = 'ade09cc3-a769-56d7-9832-a2bea14c7e71', 'ad4b5c3f-4da9-4a09-8465-831ae9a4377b'  # Scenes/BagGrid.scene
SLOTS = ['84e5c3cb-ba65-564b-9030-aef243563cf5', 'ac186d62-e963-5bdf-a85f-818a706f1872', 'aed03126-eba8-5f3b-bac7-3b2e807f7001',
         'a510f749-6eae-56a6-bfef-eee638e32ac3', '8356c951-6dd0-5800-8ff6-cd55bae9fb68', '34112067-5790-595b-bb55-86c5b5258799',
         '64ee53cc-77f9-58d4-bd4e-c306f46c82c2', 'b627f4ce-98fd-5638-8537-811405e32168', '76f8b2b4-b631-5707-b157-8bdb915bdac9',
         '29bff071-fd12-5b01-8d3c-017665e37591', '001ec5b7-cad5-5593-b57d-387d7855e316', '0b6197e1-b555-5110-984f-30007034ba0a',
         '4451663d-c9b8-532e-804d-5116da130216', 'b5cfd3df-deab-50f2-9ff1-80ba8f18d1e0', 'aec6b0cc-5b73-5e91-87f2-f39859eefc0f',
         '10c06123-30ae-57f6-91fa-295767a3bf64', '207491a0-b00e-558d-82f3-74edda8b7cba', 'bb67e322-80fb-5ef4-994b-7bc9c40155cc',
         '8a9b819d-ea97-5ae8-aac7-9f29332afec6', '94351552-74f9-57cf-ae77-d1ea38a1e26e']
FRAMES = 300
CAPTURES = [15, 30, 50, 75, 115, 150, 185, 225, 290]
MODES = ['scene-play', 'release', 'native-aot']


def center(slot):
    # Grid 4 in a 360-wide layout at (40, 76), padding and gap 6: cells 82.5 × 58, steps 88.5 and 64.
    return 46 + 88.5 * (slot % 4) + 41.25, 82 + 64 * (slot // 4) + 29


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
    # Mouse drag: herb (slot 0) onto the empty slot 5, 8 move steps.
    pointer(10, 0); pointer(11, 0, down=True)
    for i in range(1, 9): pointer(11 + i, 0, toward=5, t=i / 8)
    pointer(20, 5, down=False)
    # Mouse click to pick the brass key (slot 2), click the empty slot 3 to put it there.
    pointer(40, 2, down=True); pointer(41, 2, down=False)
    pointer(60, 3, down=True); pointer(61, 3, down=False)
    # Keyboard from slot 3: Left, Down to the bread (6), Enter picks, Right, Down to the lantern (11), Enter swaps.
    key(90, 'Left'); key(100, 'Down'); key(110, 'Return'); key(120, 'Right'); key(130, 'Down'); key(140, 'Return')
    # Pad: Left to 10, South picks it, East cancels; Down, Right to the map (15), South picks, Up, Up to 7, South drops.
    pad(170, 'Pad.DpadLeft'); pad(180, 'Pad.South'); pad(190, 'Pad.East'); pad(200, 'Pad.DpadDown'); pad(210, 'Pad.DpadRight')
    pad(220, 'Pad.South'); pad(230, 'Pad.DpadUp'); pad(240, 'Pad.DpadUp'); pad(250, 'Pad.South')
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
    scene = dest / 'Scenes/BagGrid.scene'
    slots = ', '.join(f'"{s}"' for s in SLOTS)
    scene.write_text(scene.read_text(encoding='utf-8') + '- id: 5b1c7f2e-2d4a-4e0b-8c3d-16d313000001\n  name: "Bag trace"\n  x: 0\n  y: 0\n'
                     f'  width: 1\n  height: 1\n  components:\n  - type: script\n    script: TraceBag\n    values: {{"Slots": [{slots}]}}\n',
                     encoding='utf-8')


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
    names = lambda f: at(f)[2]
    assert names(5)[0] == '약초' and names(5)[5] == '', names(5)
    assert names(25)[0] == '' and names(25)[5] == '약초', 'mouse drag swaps slot 0 and 5'
    assert at(50)[1] == '2' and names(50)[2] == '황동 열쇠', 'click picks the key up'
    assert names(70)[2] == '' and names(70)[3] == '황동 열쇠' and at(70)[1] == '-', 'click on slot 3 puts the key there'
    assert at(115)[:2] == ('6', '6'), 'keyboard picks the bread'
    assert names(150)[6] == '등불' and names(150)[11] == '빵' and at(150)[1] == '-', 'Enter on the lantern swaps them'
    assert at(185)[:2] == ('10', '10') and at(195)[1] == '-' and names(195) == names(150), 'pad East cancels without a drop'
    assert at(225)[:2] == ('15', '15') and names(260)[7] == '지도' and names(260)[15] == '' and at(260)[:2] == ('7', '-'), 'pad drop moves the map'
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
