"""Replay InventoryLab's market street (Text 2D, roadmap 16.6) in exported players and compare with Scene Play."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = 'ade09cc3-a769-56d7-9832-a2bea14c7e71', 'b7e9e2ee-5ef5-46f1-a97b-9852d3514980'  # Scenes/Labels.scene
FRAMES = 240
# No input: DamageBurst rents 100 numbers on ticks 20, 110 and 200; each rises for 60 ticks.
CAPTURES = [10, 30, 79, 100, 120, 230]
MODES = ['scene-play', 'release', 'native-aot']


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    # The editor opens the start scene; the exported players start where the recording says.
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    settings.write_text(re.sub(r'(?m)^startScene: .*$', f'startScene: {START}', text, count=1), encoding='utf-8')


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents: []\n', encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'labels.inputreplay')
    env = dict(os.environ, MONOON_USER_DATA=str(out / 'profile'),
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
    # Numbers appear after tick 20 and have risen by 79; at 100 they are back in the pool and the street looks as before.
    # Ten ticks into the second burst (120) the reused pool draws exactly what ten ticks into the first (30) drew.
    assert len({hashes[10], hashes[30], hashes[79], hashes[230]}) == 4, hashes
    assert hashes[10] == hashes[100] and hashes[30] == hashes[120], hashes
    result = dict(captures={f: h[:12] for f, h in hashes.items()})
    (out / 'result.json').write_text(json.dumps(result) + '\n', encoding='utf-8')
    print(result, flush=True)


def compare(root):
    """Scene Play and both exported players draw identical captures (one SDL_ttf rasterizer, SDL software renderer)."""
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
