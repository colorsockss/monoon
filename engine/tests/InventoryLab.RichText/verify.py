"""Replay the herbalist's rich text dialogue in exported InventoryLab players and compare the three runs."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = 'ade09cc3-a769-56d7-9832-a2bea14c7e71', '0b0b8126-2ff1-5bbb-a7f0-5ef0af802bf8'  # Showcase.scene (the herb yard)
LINE = '383f4bbc-fb27-5b52-8ea8-a7c78adb5923'  # the dialogue box's Line
FRAMES = 360
# Up (held 10 frames) walks into talking range; T opens the greeting, 4 asks where herbs grow (translated long line),
# Space skips it, 1 returns to the greeting, 1 asks about the request (icon, colour, bold and size).
HOLDS = [(0, 'Up', 10)]
EVENTS = [(10, 'T'), (90, '4'), (150, 'Space'), (170, '1'), (230, '1')]
CAPTURES = [30, 85, 120, 152, 175, 262, 345]
MODES = ['scene-play', 'release', 'native-aot']


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceReveal.source', dest / 'Scripts/TraceReveal.cs')
    scene = dest / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.count('\ndefaultCameraId:') == 1
    probe = ('- id: 2b7f2f0e-5d0c-4c35-9e0a-5a1d3c2b9f41\n  name: "Reveal trace"\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceReveal\n    values: {{"Line": "{LINE}"}}')
    scene.write_text(text.replace('\ndefaultCameraId:', '\n' + probe + '\ndefaultCameraId:', 1), encoding='utf-8')


def recording(path):
    keys = sorted([(at, key, down) for frame, key, held in HOLDS + [(f, k, 1) for f, k in EVENTS]
                   for at, down in ((frame, 'true'), (frame + held, 'false'))], key=lambda e: e[0])
    lines = [f'- frame: {at}\n  timeTicks: {at * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n' for at, key, down in keys]
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:\n' + ''.join(lines), encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Herbalist-rich.inputreplay')
    env = dict(os.environ, MONOON_REVEAL_TRACE=str(out / 'reveal.tsv'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'Herbalist-rich.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(out)


def rows(out):
    return [line.split('\t') for line in (out / 'reveal.tsv').read_text(encoding='utf-8').splitlines()]


def check(out):
    trace = rows(out)
    assert len(trace) == FRAMES, len(trace)
    shown = {int(r[0]): (int(r[1]), int(r[2]), r[3]) for r in trace}
    # Greeting: 45 characters a second (the yard's Line), complete before the next answer.
    assert shown[11][0] == 0 and 0 < shown[30][0] < shown[30][1], shown[30]
    assert shown[85][0] == shown[85][1], shown[85]
    # The translated long line is still revealing when Space skips it on frame 150.
    assert shown[149][2].startswith('Herbs grow') and shown[149][0] < shown[149][1], shown[149]
    assert shown[151][0] == shown[151][1], shown[151]
    # Returning to the greeting and asking about the request restart the reveal.
    assert shown[172][0] < shown[172][1] and shown[232][0] < shown[232][1] and shown[345][0] == shown[345][1]
    for frame in CAPTURES:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    result = dict(frames=len(trace), captures=CAPTURES, progress={f: shown[f][:2] for f in CAPTURES})
    (out / 'result.json').write_text(json.dumps(result, ensure_ascii=False) + '\n', encoding='utf-8')
    print(result, flush=True)


def compare(root):
    """Same reveal on every frame in all three runs; Scene Play and both exported players draw identical captures."""
    traces = {mode: (root / mode / 'reveal.tsv').read_bytes() for mode in MODES}
    assert len(set(traces.values())) == 1, 'reveal progress differs between runs'
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    lines = ['frame\t' + '\t'.join(MODES) + '\tidentical']
    for frame in CAPTURES:
        hashes = [digest(root / mode / 'captures' / f'{frame:04}.png') for mode in MODES]
        # All three draw text with the same SDL_ttf rasterizer on SDL's software renderer.
        assert len(set(hashes)) == 1, f'captures differ at frame {frame}: {hashes}'
        lines.append(f'{frame}\t' + '\t'.join(h[:12] for h in hashes) + '\tTrue')
    (root / 'comparison.tsv').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'check': check, 'compare': compare}[command](*(Path(p) for p in paths))
