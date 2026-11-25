"""Roadmap 16.5: the AnimatorLab showcase Guard state machine traced in the editor preview, Scene Play, Release and Native AOT,
and checked against an independent model of Assets/Showcase/Guard.animator."""
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '793807d6-c20d-4c32-982f-08aa2e216b01'
SCENE = 'cd30ffcd-4783-51d6-bf67-bbfcf9b8558d'  # Scenes/Showcase.scene
FRAMES = 320
SNAPSHOTS = [40, 95, 215]
# frame: key. Speed, attack, Any State hit, airborne Hurt, combo limit, release by reset.
PRESSES = {5: 'Right', 30: 'Space', 90: 'Up', 100: 'Down', 140: 'Down', 160: 'Left', 170: 'Space', 185: 'Space', 200: 'Space', 260: 'Left'}
STEP = struct.unpack('f', struct.pack('f', 1 / 60))[0]
LENGTH = {'Idle': 1.0, 'Walk': 0.4, 'Attack': 0.52, 'Recover': 0.3, 'Hurt': 0.7}  # the showcase Guard clips


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    for name in ('TraceGuard', 'DriveGuard'):
        shutil.copyfile(HERE / f'{name}.source', destination / f'Scripts/{name}.cs')
    assert f'startScene: {SCENE}' in (destination / 'GameSettings').read_text(encoding='utf-8')
    # The showcase GuardTrainer hits back after every third swing; the copy drives the guard with keys instead.
    scene = destination / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    trainer = re.findall(r'  - type: script\n    script: Showcase/GuardTrainer\n    values: .*\n', text)
    assert len(trainer) == 1
    scene.write_text(text.replace(trainer[0], '  - type: script\n    script: DriveGuard\n  - type: script\n    script: TraceGuard\n'), encoding='utf-8')


def recording(path):
    events = []
    for frame, key in PRESSES.items():
        for offset, down in [(0, 'true'), (1, 'false')]:
            tick = frame + offset
            events.append(f'- frame: {tick}\n  timeTicks: {tick * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n')
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + ''.join(events), encoding='utf-8')


def model():
    """Guard.animator written out by hand: (from, to, test(values), exit time). Returns the state before each tick's step."""
    p = {'Speed': 0.0, 'Grounded': 1, 'Combo': 0, 'Attack': 0, 'Hit': 0}
    rules = [('*', 'Hurt', lambda: p['Hit'], None, ['Hit']),
             ('Idle', 'Walk', lambda: p['Speed'] > 0.1 and p['Grounded'], None, []),
             ('Walk', 'Idle', lambda: p['Speed'] <= 0.1, None, []),
             ('Idle', 'Attack', lambda: p['Attack'] and p['Combo'] < 3, None, ['Attack']),
             ('Walk', 'Attack', lambda: p['Attack'] and p['Combo'] < 3, None, ['Attack']),
             ('Attack', 'Recover', lambda: True, 1, []),
             ('Recover', 'Idle', lambda: True, 1, []),
             ('Hurt', 'Idle', lambda: p['Grounded'], 1, [])]
    state, elapsed, combo, states = 'Idle', 0.0, 0, []
    for tick in range(FRAMES):
        states.append(state)
        key = PRESSES.get(tick)
        if key == 'Right': p['Speed'] = 1.0
        if key == 'Left': p['Speed'] = 0.0; combo = p['Combo'] = 0
        if key == 'Space': combo += 1; p['Combo'] = combo; p['Attack'] = 1
        if key == 'Up': p['Hit'] = 1
        if key == 'Down': p['Grounded'] = 1 - p['Grounded']
        for source, target, test, exit, triggers in rules:
            if source not in (state, '*') or source == '*' and target == state: continue
            if exit is not None and elapsed < exit * LENGTH[state]: continue
            if not test(): continue
            for t in triggers: p[t] = 0
            state, elapsed = target, 0.0
            break
        elapsed += STEP
    return states


def check(trace):
    rows = [line.split('|') for line in Path(trace).read_text(encoding='utf-8').splitlines()]
    want = model()
    expect(len(rows) == FRAMES - 1 or len(rows) == FRAMES, f'{Path(trace).name}: {len(rows)} traced ticks')
    bad = [(int(t), s, want[int(t)]) for t, s, _ in rows if s != want[int(t)]]
    expect(not bad, f'{Path(trace).name}: every tick matches the independent model' + (f' (first miss tick {bad[0][0]}: {bad[0][1]} vs {bad[0][2]})' if bad else ''))
    seen = {s for _, s, _ in rows}
    expect(seen == set(LENGTH), f'{Path(trace).name}: visits {", ".join(sorted(seen))}')


def compare(*traces):
    for t in traces: check(t)
    texts = [Path(t).read_text(encoding='utf-8') for t in traces]
    n = min(len(t.splitlines()) for t in texts)
    expect(len({'\n'.join(t.splitlines()[:n]) for t in texts}) == 1, f'{len(traces)} paths trace the same state and frame on all {n} ticks')


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
    recording(out / 'guard.inputreplay')
    env = dict(os.environ, MONOON_GUARD_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(profile),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "frames"};' + ','.join(map(str, SNAPSHOTS)),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'guard.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=120, check=True)
    check(out / f'{out.name}.trace')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'compare': compare, 'snapshots': snapshots}[command](*(Path(p) for p in paths))
