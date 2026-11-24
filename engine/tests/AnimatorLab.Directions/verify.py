"""Roadmap 16.5: AnimatorLab Scout direction sets traced in the editor preview, Scene Play, Release and Native AOT, and checked
against the independent model in scout.py."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import scout

PROJECT = scout.PROJECT
SCENE = scout.IDS['Scenes/Scout.scene']
FRAMES = 420
SNAPSHOTS = [30, 95, 240, 330]
# (frame, key, down): walk N, NE, E, SE, S, SW, W, NW, N, stop (Idle faces N), a 2-tick SW step (Idle shows S), a slow lap.
EVENTS = [(10, 'Up', 1), (35, 'Right', 1), (60, 'Up', 0), (85, 'Down', 1), (110, 'Right', 0), (135, 'Left', 1), (160, 'Down', 0),
          (185, 'Up', 1), (210, 'Left', 0), (230, 'Up', 0), (250, 'Down', 1), (250, 'Left', 1), (252, 'Down', 0), (252, 'Left', 0),
          (270, 'Space', 1), (271, 'Space', 0)]
LAP = 128  # ticks; each of the 64 square-perimeter points is held for two ticks


def sweep(i):
    """Point i of 64 on the square [-1, 1]^2, clockwise on screen from the top-right corner, in steps of 1/8 (exact in float)."""
    side, k = divmod(i, 16)
    return [(1, -1 + k / 8), (1 - k / 8, 1), (-1, 1 - k / 8), (-1 + k / 8, -1)][side]


def inputs():
    """The Move vector and Speed ScoutDemo sets on each tick (keys held from their down frame until their up frame)."""
    held, lap, out = set(), -1, []
    for tick in range(FRAMES):
        pressed = set()
        for frame, key, down in EVENTS:
            if frame == tick:
                (held.add if down else held.discard)(key)
                if down: pressed.add(key)
        if 'Space' in pressed: lap = 0 if lap < 0 else -1
        if lap >= 0:
            x, y = sweep(lap // 2)
            lap += 1
            if lap == LAP: lap = -1
        else:
            x = ('Right' in held) - ('Left' in held)
            y = ('Down' in held) - ('Up' in held)
        out.append((x, y, 0 if x == 0 and y == 0 else 1))
    return out


def model():
    """Scout.animator written out: Idle (4 directions) <-> Walk (8 directions) by Speed; (state, facing) before each step."""
    state, facing, rows = 'Idle', 4, []
    for x, y, speed in inputs():
        rows.append((state, facing))
        if state == 'Idle' and speed > 0.1: state = 'Walk'
        elif state == 'Walk' and speed <= 0.1: state = 'Idle'
        facing = scout.pick(facing, 8 if state == 'Walk' else 4, x, y)
    return rows


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(Path(__file__).resolve().parent / 'TraceScout.source', destination / 'Scripts/TraceScout.cs')
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert text.count(f'startScene: {PROJECT}') == 1
    settings.write_text(text.replace(f'startScene: {PROJECT}', f'startScene: {SCENE}'), encoding='utf-8')
    scene = destination / 'Scenes/Scout.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = f'- id: {scout.IDS["Scout camera"]}\n'  # the Scout's components end right before the camera
    assert text.count(anchor) == 1
    scene.write_text(text.replace(anchor, '  - type: script\n    script: TraceScout\n' + anchor), encoding='utf-8')


def recording(path):
    events = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{k}", down: {"true" if d else "false"}}}\n'
                     for f, k, d in EVENTS)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + events, encoding='utf-8')


def check(trace):
    rows = [line.split('|') for line in Path(trace).read_text(encoding='utf-8').splitlines()]
    want = model()
    name = Path(trace).name
    expect(len(rows) in (FRAMES - 1, FRAMES), f'{name}: {len(rows)} traced ticks')
    bad = [(int(t), s, int(f), want[int(t)]) for t, s, _, f in rows if (s, int(f)) != want[int(t)]]
    expect(not bad, f'{name}: every tick matches the independent model (state and facing)' +
           (f' (first miss tick {bad[0][0]}: {bad[0][1]} {bad[0][2]} vs {bad[0][3]})' if bad else ''))
    walk = {int(f) for _, s, _, f in rows if s == 'Walk'}
    idle = {int(f) for _, s, _, f in rows if s == 'Idle'}
    expect(walk == set(range(8)) and idle == {0, 4}, f'{name}: Walk faces all 8 directions; Idle shows only axes (N after walking N and after the lap, S at start and after the SW step)')


def compare(*traces):
    for t in traces: check(t)
    texts = [Path(t).read_text(encoding='utf-8').splitlines() for t in traces]
    n = min(map(len, texts))
    expect(len({'\n'.join(t[:n]) for t in texts}) == 1, f'{len(traces)} paths trace the same state, frame and facing on all {n} ticks')


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
    recording(out / 'scout.inputreplay')
    env = dict(os.environ, MONOON_SCOUT_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(profile),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "frames"};' + ','.join(map(str, SNAPSHOTS)),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'scout.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=120, check=True)
    check(out / f'{out.name}.trace')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'compare': compare, 'snapshots': snapshots}[command](*(Path(p) for p in paths))
