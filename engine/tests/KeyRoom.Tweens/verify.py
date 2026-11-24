"""Roadmap 16.3: KeyRoom's satchel pop (game-time tweens) and Controls press (UI-time tween) at five points in three paths."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = START = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
FRAMES = 140
# Open the chest, walk right into the coin (the satchel pops), then click the Controls button (it presses in and out).
EVENTS = [(5, "{kind: key, control: \"E\", down: true}"), (6, "{kind: key, control: \"E\", down: false}"),
          (10, "{kind: key, control: \"Right\", down: true}"), (60, "{kind: key, control: \"Right\", down: false}"),
          (100, "{kind: pointer, control: Mouse.Left, x: 742, y: 28, down: true}"),
          (101, "{kind: pointer, control: Mouse.Left, x: 742, y: 28, down: false}")]
GROW, UP, DOWN = 1.4, .1, .15
GOLD, SATCHEL = 'FFFFD65C', 'FFBB504A'


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceTweens.source', destination / 'Scripts/TraceTweens.cs')
    scene = destination / 'Scenes/KeyRoom.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '- id: be0b6e23-58d7-4ed7-a8a4-19dad1c60dc6\n  name: Key bindings opener\n'
    assert text.count(anchor) == 1
    probe = ('- id: 3e5f7a9c-1b2d-4c6e-8f0a-2b4d6f8a0c1e\n  name: Tween trace\n  width: 1\n  height: 1\n  components:\n'
             '  - type: script\n    script: TraceTweens\n')
    scene.write_text(text.replace(anchor, probe + anchor), encoding='utf-8')


def recording(path):
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {e}\n' for f, e in EVENTS)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + lines, encoding='utf-8')


def rows(path):
    out = []
    for line in path.read_text(encoding='utf-8').splitlines():
        frame, *fields = line.split('|')
        r = dict(f.split('=', 1) for f in fields)
        out.append(dict(frame=int(frame), tick=int(r['tick']), coin=r['coin'] == 'True', controls=r['controls'] == 'True',
                        pos=tuple(map(float, r['pos'].split(','))), scale=tuple(map(float, r['scale'].split(','))), argb=r['argb'],
                        button=tuple(map(float, r['button'].split(',')))))
    return out


def ease_out(t): return t * (2 - t)
def ease_in(t): return t * t


def summary(path):
    trace = rows(path)
    # Satchel: the tween starts the tick the coin flag appears; the trace shows each tick's value one tick later.
    moving = next(i for i, r in enumerate(trace) if r['scale'] != (1.0, 1.0))
    base = moving - 1
    expect(trace[base]['coin'] and not trace[base - 2]['coin'], f'{path.name}: the pop starts right after the coin is collected (frame {trace[base]["frame"]})')
    points = []
    for k in [0, 3, 6, 9, 15]:
        r = trace[base + k]
        t = k / 60
        s = 1 + (GROW - 1) * (ease_out(t / UP) if t <= UP else 1 - ease_in(min(1, (t - UP) / DOWN)))
        expect(abs(r['scale'][0] - s) < 1e-4 and r['scale'][0] == r['scale'][1] and
               abs(r['pos'][0] - (13 - 9 * (s - 1))) < 1e-3 and abs(r['pos'][1] - (13 - 8.5 * (s - 1))) < 1e-3,
               f'{path.name}: satchel at +{k} ticks has scale {r["scale"][0]:.5f} (expected {s:.5f}) about its centre')
        points.append(dict(object='satchel', ticks=k, x=r['pos'][0], y=r['pos'][1], scale=r['scale'][0], argb=r['argb']))
    expect(points[0]['argb'] == SATCHEL and points[2]['argb'] == GOLD and points[4]['argb'] == SATCHEL,
           f'{path.name}: the satchel flashes gold at the peak and returns to its colour')
    # Controls button: the press starts on the frame of the click; UI time runs one frame (1/60 s) per replay frame.
    click = next(i for i, r in enumerate(trace) if r['controls'] != trace[0]['controls'])
    rest = trace[click - 1]['button']
    for k in [0, 1, 3, 5, 8]:
        r = trace[click + k]
        t = k / 60
        inset = 2 * (ease_out(min(1, t / .05)) if t <= .05 + 1e-9 else 1 - ease_in(min(1, (t - .05) / .08)))
        want = (rest[0] + inset, rest[1] + inset, rest[2] - 2 * inset, rest[3] - 2 * inset)
        expect(all(abs(a - b) < 1e-3 for a, b in zip(r['button'], want)), f'{path.name}: Controls button at +{k} frames is inset {inset:.4f}')
        points.append(dict(object='button', frames=k, box=list(r['button'])))
    return points


def compare(*traces):
    results = [summary(Path(t)) for t in traces]
    texts = [Path(t).read_text(encoding='utf-8') for t in traces]
    expect(len(set(texts)) == 1, 'Scene Play, Release and Native AOT traces are identical, bit for bit')
    (Path(traces[0]).parent / 'points.json').write_text(json.dumps(results[0], indent=2) + '\n')


def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    profile = out / 'profile'
    profile.mkdir()
    recording(out / 'tweens.inputreplay')
    env = dict(os.environ, MONOON_TWEEN_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(profile),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'tweens.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=120, check=True)
    summary(out / f'{out.name}.trace')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'compare': compare}[command](*(Path(p) for p in paths))
