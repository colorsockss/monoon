"""Replay KeyRoom's door hint wait with a pause and single steps, and check it lands on the same game tick everywhere."""
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = START = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
FRAMES = 120
# Walk left onto the key (door opens, 0.5 s wait starts), pause during the wait, step three ticks, resume.
EVENTS = [(5, 'Left', True), (22, 'Left', False),
          (28, 'P', True), (29, 'P', False),
          (40, 'Period', True), (41, 'Period', False),
          (46, 'Period', True), (47, 'Period', False),
          (52, 'Period', True), (53, 'Period', False),
          (70, 'P', True), (71, 'P', False)]


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceWaits.source', destination / 'Scripts/TraceWaits.cs')
    scene = destination / 'Scenes/KeyRoom.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '- id: be0b6e23-58d7-4ed7-a8a4-19dad1c60dc6\n  name: Key bindings opener\n'
    assert text.count(anchor) == 1
    probe = ('- id: 2b7c9e14-3f60-4d8a-9c21-7e5f0a3d6b48\n  name: Wait trace\n  width: 1\n  height: 1\n  components:\n'
             '  - type: script\n    script: TraceWaits\n')
    scene.write_text(text.replace(anchor, probe + anchor), encoding='utf-8')


def recording(path):
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{c}", down: {str(d).lower()}}}\n'
                    for f, c, d in EVENTS)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + lines, encoding='utf-8')


def trace(path):
    rows = []
    for line in path.read_text(encoding='utf-8').splitlines():
        frame, *fields = line.split('|')
        row = dict(f.split('=', 1) for f in fields)
        rows.append((int(frame), int(row['tick']), row['paused'] == 'True', row['key'] == 'True', row['hint'] == 'True'))
    return rows


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def summary(path):
    rows = trace(path)
    key_frame, key_tick = next((f, t) for f, t, _, k, _ in rows if k)
    hint_frame, hint_tick = next((f, t) for f, t, _, _, h in rows if h)
    paused = [(f, t, h) for f, t, p, _, h in rows if p]
    expect(hint_tick - key_tick == 30, f'{path.stem}: hint {hint_tick - key_tick} game ticks after the key (expected 30)')
    expect(paused and not any(h for _, _, h in paused) and key_tick < paused[0][1] < hint_tick,
           f'{path.stem}: paused at tick {paused[0][1]} inside the wait, no hint while paused')
    steps = paused[-1][1] - paused[0][1]
    expect(steps == 3, f'{path.stem}: three single steps advanced {steps} ticks while paused')
    expect(hint_frame - key_frame > 30, f'{path.stem}: the hint frame {hint_frame} is later than key frame {key_frame} + 30 because of the pause')
    return key_frame, key_tick, hint_frame, hint_tick


def compare(*traces):
    results = [summary(Path(t)) for t in traces]
    for t, (kf, kt, hf, ht) in zip(traces, results):
        print(f'{Path(t).stem}: key frame {kf} tick {kt}, hint frame {hf} tick {ht}')
    expect(len(set(results)) == 1, 'all runs show the key and the hint on the same frame and game tick')


def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    profile = out / 'profile'
    profile.mkdir()
    recording(out / 'wait.inputreplay')
    env = dict(os.environ, MONOON_WAIT_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(profile),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'wait.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=60, check=True)
    summary(out / f'{out.name}.trace')


if __name__ == '__main__':
    if sys.argv[1] == 'compare':
        compare(*sys.argv[2:])
    else:
        {'prepare': prepare, 'play': play}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
