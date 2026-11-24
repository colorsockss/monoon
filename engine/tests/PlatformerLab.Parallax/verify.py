"""Roadmap 16.2: PlatformerLab's three parallax layers under a moving camera, traced in Scene Play, Release and Native AOT."""
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '3b309c28-75ce-562e-92ac-f94ee48b18c7'
SCENE = 'b58454d1-1dc3-5f31-a496-cd03cf3983b2'
FRAMES = 120
SNAPSHOTS = [0, 30, 60, 119]
SCROLL = {'sky': 0.0, 'hills': 0.25, 'trees': 0.6}


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceParallax.source', destination / 'Scripts/TraceParallax.cs')
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert text.count('startScene: 4fe9f1eb-5dcd-5f90-a033-78a61e876a0d') == 1
    settings.write_text(text.replace('startScene: 4fe9f1eb-5dcd-5f90-a033-78a61e876a0d', f'startScene: {SCENE}'), encoding='utf-8')
    scene = destination / 'Scenes/Parallax.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '- id: de22555b-b604-55df-bd6c-cbfdc53c089a\n  name: Canvas\n'
    assert text.count(anchor) == 1
    probe = ('- id: 9a1d6c52-7b0e-4f3a-8e21-5c4d3b2a1f09\n  name: Parallax trace\n  width: 1\n  height: 1\n  components:\n'
             '  - type: script\n    script: TraceParallax\n')
    scene.write_text(text.replace(anchor, probe + anchor), encoding='utf-8')


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents: []\n', encoding='utf-8')


def summary(path):
    rows = []
    for line in path.read_text(encoding='utf-8').splitlines():
        frame, *fields = line.split('|')
        row = dict(f.split('=', 1) for f in fields)
        rows.append((int(frame), int(row['tick']), float(row['camera']),
                     {k: tuple(map(float, row[k].split(','))) for k in SCROLL}))
    expect(len(rows) >= FRAMES - 1, f'{path.name}: {len(rows)} traced ticks')
    travel = rows[-1][2] - rows[0][2]
    expect(travel > 100, f'{path.name}: the camera travels {travel:.1f} units')
    for _, tick, camera, offsets in rows:
        moved = camera - 400
        for name, scroll in SCROLL.items():
            x, y = offsets[name]
            if abs(x - moved * (1 - scroll)) > 1e-3 or y != 0:
                raise SystemExit(f'FAIL {path.name} tick {tick}: {name} offset {x} for travel {moved} at scroll {scroll}')
    print(f'PASS {path.name}: every layer moves (1 - scroll) x the camera travel; on screen sky 0, hills 0.25, trees 0.6 of it', flush=True)
    return rows


def compare(*traces):
    texts = [Path(t).read_text(encoding='utf-8') for t in traces]
    for t in traces: summary(Path(t))
    expect(len(set(texts)) == 1, 'Scene Play, Release and Native AOT trace the same camera and layer offsets, bit for bit')


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
    recording(out / 'parallax.inputreplay')
    env = dict(os.environ, MONOON_PARALLAX_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(profile),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "frames"};' + ','.join(map(str, SNAPSHOTS)),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'parallax.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=120, check=True)
    summary(out / f'{out.name}.trace')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'compare': compare, 'snapshots': snapshots}[command](*(Path(p) for p in paths))
