"""CameraLab's framing showcase (Scenes/ShowcaseFraming.scene): dead zone, look-ahead and group framing (roadmap 16.11).

prepare <sample> <dest>   copy CameraLab, start it in the framing showcase as it is (FramingTour walks the hero and the
                          partner and switches from the Follow camera to the Frame camera) and add TraceCameras
play <MoNoonPlayer> <out> headless replay without input; the trace has one line per game tick
check <out>...            the traces are byte-identical, and the camera behaviour holds in the first one
"""
import os, re, shutil, subprocess, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = 'cf624488-489b-5951-a279-fffa22d99f7a'
FRAMING = '7d2a7ef4-c64e-583e-a2b9-8c5f45b0aadf'
FRAMES = 1000   # the tour ends with both walkers at the herbalist by tick ~960
HALF = 11, 6   # the walkers' feet box is 22 x 12; cameras follow its centre


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceCameras.source', destination / 'Scripts/TraceCameras.cs')
    settings = destination / 'GameSettings'
    settings.write_text(re.sub(r'(?m)^startScene: .*$', f'startScene: {FRAMING}', settings.read_text(encoding='utf-8'), count=1), encoding='utf-8')
    scene = destination / 'Scenes/ShowcaseFraming.scene'
    text = scene.read_text(encoding='utf-8')
    ids = {m.group(2): m.group(1) for m in re.finditer(r'(?m)^- id: (\S+)\n(?:  parentId: \S+\n)?  name: (.*)$', text)}
    trace = ('- id: 3c0e6a51-2b9d-4c4e-9f0a-6d1f7c2a9b10\n  name: Camera trace\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceCameras\n    values:\n      Main: "{ids["Follow Camera"]}"\n      Second: "{ids["Frame Camera"]}"\n'
             f'      Player: "{ids["Player"]}"\n      Partner: "{ids["Partner"]}"\n')
    assert text.count('defaultCameraId:') == 1
    scene.write_text(text.replace('defaultCameraId:', trace + 'defaultCameraId:'), encoding='utf-8')


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {FRAMING}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents: []\n', encoding='utf-8')


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
    nums = lambda s: None if s == '-' else [float(v) for v in s.split(',')]
    for line in trace.read_text(encoding='utf-8').splitlines():
        tick, p, q, a, b = line.split('|')
        yield int(tick), nums(p), nums(q), nums(a), nums(b)


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
    ticks = sorted(t)
    centre = lambda p: (p[0] + HALF[0], p[1] + HALF[1])
    # Dead zone (120 x 80 world units): the hero picks three herbs around the camp; the Follow camera does not move.
    switch = min(k for k in ticks if t[k][3] is not None)
    picks = [k for k in ticks if k < 280]
    spread = max(abs(centre(t[k][0])[0] - centre(t[picks[0]][0])[0]) + abs(centre(t[k][0])[1] - centre(t[picks[0]][0])[1]) for k in picks)
    expect(spread > 40 and len({tuple(t[k][2]) for k in picks}) == 1,
           f'dead zone: the hero walks up to {spread:.0f} units around the camp while the Follow camera holds at {t[picks[0]][2][:2]}')
    # Look-ahead (0.25 s): on the trail east the camera starts moving while the hero is still inside the dead zone.
    moved = next(k for k in ticks if k > picks[-1] and t[k][2][0] != t[k - 1][2][0])
    hero = centre(t[moved][0])[0]
    expect(hero < t[moved - 1][2][0] + 60, f'look-ahead: the Follow camera moves on tick {moved} with the hero {hero - t[moved - 1][2][0]:.1f} units from its centre (dead zone edge 60)')
    trail = [k for k in ticks if moved < k < switch]
    expect(trail and all(t[k][2][0] > t[k - 1][2][0] for k in trail), f'the Follow camera follows the hero east on ticks {trail[0]}–{trail[-1]}')
    # Group framing: the Frame camera takes over; it widens while the partner is far off and closes in as they meet.
    expect(all(t[k][3] is None for k in ticks if k < switch) and all(t[k][3] is not None for k in ticks if k >= switch), f'the Frame camera runs from tick {switch}')
    zooms = [t[k][3][2] for k in ticks if k >= switch]
    expect(min(zooms) < 1.45 and zooms[-1] > 1.49, f'group framing zooms out to {min(zooms):.3f} and back to {zooms[-1]:.3f}')
    expect(all(450 / 896 - 1e-3 <= z <= 1.5 + 1e-6 for z in zooms), 'framing zoom stays between the bounds fit and the camera Zoom 1.5')
    a, b = centre(t[ticks[-1]][0]), centre(t[ticks[-1]][1])
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    end = t[ticks[-1]][3]
    expect(abs(end[0] - mid[0]) < 2 and abs(end[1] - mid[1]) < 2, f'at the herbalist the Frame camera centres the pair: {end[:2]} vs midpoint {mid}')

if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
