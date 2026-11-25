"""Replay InventoryLab's herb yard (Text 2D, roadmap 16.6) in exported players and compare with Scene Play."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = 'ade09cc3-a769-56d7-9832-a2bea14c7e71', '0b0b8126-2ff1-5bbb-a7f0-5ef0af802bf8'  # Scenes/Showcase.scene
NUMBER = 'bc54ecea-34dc-460f-b0a4-3a94f644ac48'  # Assets/Labels/DamageNumber.prefab, the pooled "+1"
FRAMES = 240
# Right walks over the second herb (picked about frame 80; its "+1" rises for 60 ticks and goes back to the pool), Up and
# Left over the first herb once the pool has it back (about frame 165): the second "+1" reuses the first label object.
HOLDS = [(10, 'Right', 80), (100, 'Up', 20), (160, 'Left', 10)]
CAPTURES = [5, 85, 120, 150, 175, 235]
MODES = ['scene-play', 'release', 'native-aot']
TRACE = ('- id: 0b000000-0000-4000-8000-0000000016e6\n  name: "Pool trace"\n  x: 0\n  y: 0\n  width: 1\n  height: 1\n  components:\n'
         f'  - type: script\n    script: TracePool\n    values: {{"Number": {{"Id": "{NUMBER}"}}}}')


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    # The editor opens the start scene; the exported players start where the recording says.
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    settings.write_text(re.sub(r'(?m)^startScene: .*$', f'startScene: {START}', text, count=1), encoding='utf-8')
    shutil.copyfile(HERE / 'TracePool.source', dest / 'Scripts/TracePool.cs')
    scene = dest / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.count('\ndefaultCameraId:') == 1
    scene.write_text(text.replace('\ndefaultCameraId:', '\n' + TRACE + '\ndefaultCameraId:', 1), encoding='utf-8')


def recording(path):
    keys = sorted([(at, key, down) for frame, key, held in HOLDS for at, down in ((frame, 'true'), (frame + held, 'false'))], key=lambda e: e[0])
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) + ']\nevents:\n'
                    + ''.join(f'- frame: {at}\n  timeTicks: {at * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n'
                              for at, key, down in keys), encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'labels.inputreplay')
    env = dict(os.environ, MONOON_USER_DATA=str(out / 'profile'), MONOON_POOL_TRACE=str(out / 'pool.tsv'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'labels.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(out)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(out):
    shots = {f: out / 'captures' / f'{f:04}.png' for f in CAPTURES}
    for frame, shot in shots.items():
        assert shot.stat().st_size > 1000, frame
    hashes = {f: digest(p) for f, p in shots.items()}
    assert len(set(hashes.values())) == len(CAPTURES), hashes
    # frame, active, available, created, reused, returned: one label object serves both pickups.
    pool = [tuple(map(int, line.split('\t'))) for line in (out / 'pool.tsv').read_text(encoding='utf-8').splitlines()]
    first = next(row[0] for row in pool if row[1] == 1)
    second = next(row[0] for row in pool if row[0] > first and row[1] == 1)
    assert 60 < first < 100 and 140 < second < 200 and pool[-1][1:] == (0, 1, 1, 1, 2), pool
    result = dict(captures={f: h[:12] for f, h in hashes.items()}, pops=[first, second], pool=pool[-1][1:])
    (out / 'result.json').write_text(json.dumps(result) + '\n', encoding='utf-8')
    print(result, flush=True)


def compare(root):
    """Scene Play and both exported players draw identical captures (one SDL_ttf rasterizer, SDL software renderer)."""
    traces = {mode: (root / mode / 'pool.tsv').read_bytes() for mode in MODES}
    assert len(set(traces.values())) == 1, 'pool traces differ between runs'
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
