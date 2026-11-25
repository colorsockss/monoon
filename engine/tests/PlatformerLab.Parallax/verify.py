"""Roadmap 16.2: PlatformerLab's showcase parallax layers under a following camera, traced in Scene Play, Release and Native AOT.

The showcase (Scenes/Showcase.scene, the start scene) has three layers: the sky backing fixed to the screen (scroll 0), the moon
(0.05) and the sky picture (0.15), all with scrollY 0. The recording holds → so the camera follows the runner east.
"""
import os, re
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '3b309c28-75ce-562e-92ac-f94ee48b18c7'
SCENE = '40845ca7-0281-5a45-878a-c1da3b04f822'
FRAMES = 120
EVENTS = [(5, 'Right', 'true'), (115, 'Right', 'false')]
SNAPSHOTS = [0, 30, 60, 119]
SCROLL = {'back': 0.0, 'moon': 0.05, 'sky': 0.15}
ZOOM = 3   # PlatformerLab uses integer scale: offsets land on whole screen pixels, 1/3 unit at zoom 3


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceParallax.source', destination / 'Scripts/TraceParallax.cs')
    scene = destination / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    ids = {m.group(2): m.group(1) for m in re.finditer(r'(?m)^- id: (\S+)\n(?:  parentId: \S+\n)?  name: (.*)$', text)}
    probe = ('- id: 9a1d6c52-7b0e-4f3a-8e21-5c4d3b2a1f09\n  name: Parallax trace\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceParallax\n    values: {{"Camera": "{ids["Camera"]}", "Back": "{ids["Sky backing"]}", '
             f'"Moon": "{ids["Moon"]}", "Sky": "{ids["Sky"]}"}}\n')
    assert text.count('defaultCameraId:') == 1
    scene.write_text(text.replace('defaultCameraId:', probe + 'defaultCameraId:'), encoding='utf-8')


def recording(path):
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{k}", down: {d}}}\n' for f, k, d in EVENTS)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + lines, encoding='utf-8')


def summary(path):
    rows = []
    for line in path.read_text(encoding='utf-8').splitlines():
        frame, *fields = line.split('|')
        row = dict(f.split('=', 1) for f in fields)
        rows.append((int(frame), int(row['tick']), tuple(map(float, row['camera'].split(','))),
                     {k: tuple(map(float, row[k].split(','))) for k in SCROLL}))
    expect(len(rows) >= FRAMES - 1, f'{path.name}: {len(rows)} traced ticks')
    start = rows[0][2]
    travel = rows[-1][2][0] - start[0]
    expect(travel > 100, f'{path.name}: the camera travels {travel:.1f} units')
    for _, tick, camera, offsets in rows:
        moved = (camera[0] - start[0], camera[1] - start[1])
        for name, scroll in SCROLL.items():
            x, y = offsets[name]
            # Each layer keeps the screen place it had on the first frame: it moves (1 - scroll) x the camera, and with
            # scrollY 0 all of the vertical travel.
            if abs(x - moved[0] * (1 - scroll)) > 1 / ZOOM or abs(y - moved[1]) > 1 / ZOOM:   # within one screen pixel
                raise SystemExit(f'FAIL {path.name} tick {tick}: {name} offset {x},{y} for travel {moved} at scroll {scroll}')
    print(f'PASS {path.name}: every layer moves (1 - scroll) x the camera travel; on screen back 0, moon 0.05, sky 0.15 of it', flush=True)
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
