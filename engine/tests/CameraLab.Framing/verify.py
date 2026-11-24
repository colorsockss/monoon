"""CameraLab dead zone, look-ahead and group framing (roadmap 16.11) in a disposable copy.

prepare <sample> <dest>   copy CameraLab: Player Camera gets a 120x80 dead zone and 0.25 s look-ahead; Partner Camera frames
                          the object group Players (both squares) with 48 px padding instead of following one; add TraceCameras.
play <MoNoonPlayer> <out> headless replay; the trace has one line per game tick
check <out>...            the traces are byte-identical, and the camera behaviour holds in the first one
"""
import os, shutil, subprocess, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = 'cf624488-489b-5951-a279-fffa22d99f7a'
MAIN, SECOND = '9220cdc4-76fa-5e2b-9107-5f4aef99a67e', '44e4c6f9-a8d1-5d37-a753-a54a9fdb7a39'
PLAYER, PARTNER = '45c4c558-3fb3-5861-88e3-dfd8a5c5bae3', 'f81d520a-421d-5c56-8abd-793db0413f22'
FRAMES = 520
# Space: automatic trip to the south-east corner (150 ticks). Then held arrows, then Space back to the north-west.
KEYS = [(5, 6, 'Space'), (180, 240, 'Left'), (250, 280, 'Up'), (290, 296, 'Right'), (320, 321, 'Space')]


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceCameras.source', destination / 'Scripts/TraceCameras.cs')
    scene = destination / 'Scenes/CameraLab.scene'
    text = scene.read_text(encoding='utf-8')
    def swap(old, new):
        nonlocal text
        assert text.count(old) == 1, old
        text = text.replace(old, new)
    swap(f'    follow: {PLAYER}\n    halfLife: 0.16\n',
         f'    follow: {PLAYER}\n    halfLife: 0.16\n    deadZoneWidth: 120\n    deadZoneHeight: 80\n    lookAhead: 0.25\n')
    swap(f'    follow: {PARTNER}\n    halfLife: 0.16\n', '    halfLife: 0.16\n    frameGroup: Players\n    framePadding: 48\n')
    for oid, name in ((PLAYER, 'Player'), (PARTNER, 'Partner')):
        swap(f'- id: {oid}\n  name: {name}\n', f'- id: {oid}\n  name: {name}\n  groups:\n  - Players\n')
    trace = ('- id: 3c0e6a51-2b9d-4c4e-9f0a-6d1f7c2a9b10\n  name: Camera trace\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceCameras\n    values:\n      Main: "{MAIN}"\n      Second: "{SECOND}"\n'
             f'      Player: "{PLAYER}"\n      Partner: "{PARTNER}"\n')
    swap('defaultCameraId:', trace + 'defaultCameraId:')
    scene.write_text(text, encoding='utf-8')


def recording(path):
    lines = []
    for down, up, key in KEYS:
        for frame, state in ((down, 'true'), (up, 'false')):
            lines.append((frame, f'- frame: {frame}\n  timeTicks: {frame * 166667}\n  input: {{kind: key, control: "{key}", down: {state}}}\n'))
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {PROJECT}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:\n' + ''.join(l for _, l in sorted(lines, key=lambda x: x[0])), encoding='utf-8')


def play(player, out):
    out = out.resolve(); out.mkdir(parents=True)
    recording(out / 'framing.inputreplay')
    env = dict(os.environ, MONOON_CAMERA_TRACE=str(out / 'framing.trace'), MONOON_USER_DATA=str(out / 'profile'),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'play.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'framing.inputreplay'), str(out / 'profile')],
                       env=env, stdout=log, stderr=log, timeout=300, check=True)
    check(out)


def rows(trace):
    for line in trace.read_text(encoding='utf-8').splitlines():
        tick, p, q, a, b = line.split('|')
        yield int(tick), [float(v) for v in p.split(',')], [float(v) for v in q.split(',')], \
            [float(v) for v in a.split(',')], [float(v) for v in b.split(',')]


def expect(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok: raise SystemExit(1)


def check(*outs):
    # Scene Play keeps running for a moment after the recording ends; compare the ticks every run has.
    traces = [(o / 'framing.trace').read_text(encoding='utf-8').splitlines(keepends=True) for o in outs]
    common = min(map(len, traces))
    for o, trace in zip(outs[1:], traces[1:]):
        expect(trace[:common] == traces[0][:common], f'{o.name}: first {common} ticks of the camera trace match {outs[0].name} byte for byte')
    base = outs[0] / 'framing.trace'
    t = {tick: (p, q, a, b) for tick, p, q, a, b in rows(base)}
    expect(len(t) >= FRAMES - 30, f'{len(t)} ticks traced')
    # Manual movement starts at rest in the south-east corner. Left: the first ticks stay inside the 120 px dead zone.
    start = min(k for k in t if k > 175 and t[k][0][0] < t[k - 1][0][0])
    still = [k for k in range(start, start + 30) if abs(t[k][2][0] - t[start - 1][2][0]) < 1e-3]
    expect(len(still) >= 5, f'dead zone: the main camera holds while the player walks {len(still)} ticks left from rest')
    # Look-ahead: the camera starts moving while the player is still inside the dead zone, pulled by the lead.
    moved = next(k for k in range(start, start + 60) if t[k][2][0] != t[k - 1][2][0])
    gap = t[moved][2][0] - (t[moved][0][0] + 16)
    expect(abs(t[moved - 1][2][0] - (t[moved][0][0] + 16)) < 60, f'look-ahead: camera moves on tick {moved}, player only {gap:.1f} px from its centre')
    zooms = [t[k][3][2] for k in sorted(t)]
    expect(min(zooms) < .95 and max(zooms) > min(zooms) + .2, f'group framing zooms between {min(zooms):.3f} and {max(zooms):.3f}')
    expect(all(z <= 1 + 1e-6 and z >= 400 / 896 - 1e-3 for z in zooms), 'framing zoom stays between the bounds fit and the camera Zoom')
    mid = min(t, key=lambda k: abs(t[k][0][0] - t[k][1][0]))
    window = [k for k in range(mid - 40, mid + 41) if k in t]
    peak = max(window, key=lambda k: t[k][3][2])
    expect(mid <= peak <= mid + 15 and t[peak][3][2] > .8 and t[mid + 40][3][2] < .6,
           f'players cross at tick {mid}: the partner view eases in to {t[peak][3][2]:.3f} (tick {peak}) and out to {t[mid + 40][3][2]:.3f}')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
