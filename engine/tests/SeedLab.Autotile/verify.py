"""Roadmap 16.9: the SeedLab route showcase's hedges (47 blob shapes), flower tufts (random) and torches and river ripples
(frame-animated), traced in Scene Play, Release and Native AOT and checked against the independent oracle in garden.py."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import garden

HERE = Path(__file__).resolve().parent
PROJECT = garden.PROJECT
SCENE = garden.SCENE
FRAMES = 50
SNAPSHOTS = [5, 25, 45]
EDITS = {10: [('hedges', 5, 3, 0), ('hedges', 6, 4, 1), ('hedges', 0, 0, 0), ('decor', 4, 2, 0), ('decor', 8, 0, 0)],
         30: [('hedges', 5, 3, 1), ('hedges', 6, 4, 0), ('decor', 4, 2, garden.FLOWERS), ('decor', 8, 0, garden.RIPPLE)]}


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceAutotile.source', destination / 'Scripts/TraceAutotile.cs')
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    start = 'startScene: cdfa4adc-9fd0-58dc-bdff-271497eca1f0'
    assert text.count(start) == 1
    settings.write_text(text.replace(start, f'startScene: {SCENE}'), encoding='utf-8')
    scene = destination / 'Scenes/ShowcaseRoute.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = f'- id: {garden.CANVAS}\n'
    assert text.count(anchor) == 1
    probe = ('- id: 4d1f0c2e-6b7a-4c39-9e58-2a0b1c3d4e5f\n  name: Autotile trace\n  width: 1\n  height: 1\n  components:\n'
             '  - type: script\n    script: TraceAutotile\n')
    scene.write_text(text.replace(anchor, probe + anchor), encoding='utf-8')


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents: []\n', encoding='utf-8')


def check(trace):
    layers = {'hedges': dict(garden.HEDGES), 'decor': dict(garden.DECOR)}
    rows = [line.split('|') for line in Path(trace).read_text(encoding='utf-8').splitlines()]
    clock = [r for r in rows if r[1] == 'clock']
    rows = [r for r in rows if r[1] != 'clock']
    expect([(int(t), n) for t, n, _ in rows] == [(t, n) for t in (1, 20, 40) for n in ('hedges', 'decor')], f'{Path(trace).name}: three snapshots of the two layers')
    want = [f'{garden.frame(garden.RIPPLE, t)},{garden.frame(garden.TORCH, t)}' for t in range(1, len(clock) + 1)]
    expect([int(t) for t, _, _ in clock] == list(range(1, len(clock) + 1)) and len(clock) >= FRAMES - 2 and [v for _, _, v in clock] == want,
           f'{Path(trace).name}: ripples (3 frames, 4 FPS) and torch (4 frames, 8 FPS) show the oracle frame on all {len(clock)} ticks')
    for tick, name, values in rows:
        tick = int(tick)
        cells = dict(layers[name])
        for at, edits in EDITS.items():
            if at < tick:
                for layer, x, y, tile in edits:
                    if layer == name:
                        if tile: cells[(x, y)] = tile
                        else: cells.pop((x, y), None)
        got = list(map(int, values.split(',')))
        want = [garden.frame(garden.display(cells, x, y, cells[(x, y)]), tick) if (x, y) in cells else 0 for y in range(garden.H) for x in range(garden.W)]
        bad = [(i % garden.W, i // garden.W, g, w) for i, (g, w) in enumerate(zip(got, want)) if g != w]
        expect(not bad, f'{Path(trace).name} tick {tick} {name}: {len(got)} cells match the oracle' + (f' (first miss {bad[0]})' if bad else ''))
    return rows


def compare(*traces):
    for t in traces: check(t)
    lines = [Path(t).read_text(encoding='utf-8').splitlines() for t in traces]
    n = min(map(len, lines))
    expect(len({'\n'.join(t[:n]) for t in lines}) == 1, f'Scene Play, Release and Native AOT resolve the same art for every cell and animation frame ({n} lines), before and after edits')


def snapshots(release, aot):
    for frame in SNAPSHOTS:
        a, b = (Path(d) / 'frames' / f'{frame:04d}.png' for d in (release, aot))
        expect(a.read_bytes() == b.read_bytes(), f'Release and Native AOT frame {frame} PNGs are identical')


def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    profile = out / 'profile'
    profile.mkdir()
    (out / 'frames').mkdir()
    recording(out / 'autotile.inputreplay')
    env = dict(os.environ, MONOON_AUTOTILE_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(profile),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "frames"};' + ','.join(map(str, SNAPSHOTS)),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'autotile.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=120, check=True)
    check(out / f'{out.name}.trace')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'compare': compare, 'snapshots': snapshots}[command](*(Path(p) for p in paths))
