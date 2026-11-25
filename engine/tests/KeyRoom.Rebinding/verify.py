"""Replay the KeyRoom moves showcase's controls menu (K) in an exported player and check that a restart keeps the chosen keys."""
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7', '061c6369-02fc-513b-8292-fd6ac88c4ea2'   # ShowcaseMoves.scene
HINT = 'bb32d279-a345-5440-9187-9a39b0567bf5'   # the caption's controls line, which MoveTrainer keeps up to date
# Centre of Restore defaults on the 800x450 screen: panel (180, 84) + padding and five rows above it.
RESTORE = (270, 328)
FRAMES = 200

# Each run is a new game process on the same profile. The menu opens on Charge's key cell.
RUNS = {
    'rebind': [(5, 'key', 'K'), (70, 'key', 'Down'), (75, 'key', 'Down'),     # down to Dash's key cell
               (80, 'key', 'Return'), (90, 'key', 'J'),                        # swap Dash O <-> Charge J
               (100, 'key', 'Return'), (110, 'key', 'Escape'),                 # cancel a keyboard capture
               (116, 'key', 'Right'), (120, 'key', 'Return'), (126, 'pad', 'Pad.North'),  # swap Dash's pad cell with Room guide's North
               (140, 'key', 'K'),                                              # close
               (150, 'key', 'O'), (155, 'key', 'O'), (180, 'key', 'J'), (185, 'key', 'J')],  # O is Charge now (no dash), J J dashes
    'restart': [(10, 'key', 'O'), (15, 'key', 'O'), (30, 'key', 'J'), (35, 'key', 'J'),   # saved keys: O O nothing, J J dashes
                (50, 'key', 'K'), (120, 'click', RESTORE), (140, 'key', 'K')],
    'restored': [(10, 'key', 'O'), (15, 'key', 'O')],
}


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceBindings.source', destination / 'Scripts/TraceBindings.cs')
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    start = next(l for l in text.splitlines() if l.startswith('startScene: '))
    settings.write_text(text.replace(start, 'startScene: ' + START), encoding='utf-8')
    scene = destination / 'Scenes/ShowcaseMoves.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '\ndefaultCameraId: '
    assert text.count(anchor) == 1
    probe = ('\n- id: 5f1d0c41-7a43-4f0e-9a51-2c7e5d0b7e11\n  name: Rebinding trace\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceBindings\n    values:\n      Hint: "{HINT}"')
    scene.write_text(text.replace(anchor, probe + anchor), encoding='utf-8')


def recording(path, events):
    lines = []
    def add(frame, body):
        lines.append(f'- frame: {frame}\n  timeTicks: {frame * 166667}\n  input: {body}\n')
    for frame, kind, value in events:
        if kind == 'key':
            add(frame, f'{{kind: key, control: "{value}", down: true}}')
            add(frame + 1, f'{{kind: key, control: "{value}", down: false}}')
        elif kind == 'pad':
            add(frame, f'{{kind: pad, control: "{value}", value: 1}}')
            add(frame + 1, f'{{kind: pad, control: "{value}", value: 0}}')
        else:
            x, y = value
            add(frame, f'{{kind: pointer, x: {x}, y: {y}}}')
            add(frame, f'{{kind: pointer, control: "Mouse.Left", down: true, x: {x}, y: {y}}}')
            add(frame + 1, f'{{kind: pointer, control: "Mouse.Left", down: false, x: {x}, y: {y}}}')
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + ''.join(lines), encoding='utf-8')


def trace(path):
    rows = []
    for line in path.read_text(encoding='utf-8').splitlines():
        frame, *fields = line.split('|')
        rows.append((int(frame), dict(f.split('=', 1) for f in fields)))
    return rows


def expect(ok, message):
    if not ok:
        raise SystemExit('FAIL ' + message)
    print('PASS ' + message, flush=True)


def check(out):
    run = trace(out / 'rebind.trace')
    expect('Dash  O O' in run[0][1]['hint'] and run[0][1]['interact'] == 'False', 'first run starts with the project keys (Dash O O)')
    expect(any(r['capturing'] == 'Dash' for _, r in run), 'Return on Dash\'s key cell starts capture')
    expect(any(r['dash'] == 'J,Pad.RightTrigger' and r['charge'] == 'O,Pad.LeftShoulder' and 'Dash  J J' in r['hint'] for _, r in run),
           'J swaps with Charge and the caption shows J')
    expect(any(r['dash'] == 'J,Pad.North' and r['guide'] == 'M,Pad.RightTrigger' for _, r in run),
           'pad capture swaps North with Room guide, which takes the right trigger')
    expect(not any('Escape' in r['dash'] for _, r in run), 'Esc cancels instead of binding')
    dashed = next(frame for frame, r in run if r['interact'] == 'True')
    expect(dashed > 180 and run[-1][1]['rebind'] == 'True', f'O O (now Charge) does not dash; J J does (frame {dashed}); the rebind is announced')
    saved = (out / 'rebind.player.settings').read_text(encoding='utf-8')
    expect('inputBindings' in saved and 'Pad.North' in saved and 'Charge' in saved, 'player.settings stores the changed actions')

    restart = trace(out / 'restart.trace')
    first = restart[0][1]
    # The bindings load in OnCreate; MoveTrainer rewrites the caption on its first fixed tick, before the first input (frame 10).
    expect(first['dash'] == 'J,Pad.North' and first['charge'] == 'O,Pad.LeftShoulder' and
           any('Dash  J J' in r['hint'] for f, r in restart if f < 10), 'restart applies the saved bindings and caption before any input')
    dashed = next(frame for frame, r in restart if r['interact'] == 'True')
    expect(dashed > 30, f'O O at frames 10-15 does nothing; J J at 30-35 dashes (frame {dashed})')
    expect(restart[-1][1]['dash'] == 'O,Pad.RightTrigger' and 'Dash  O O' in restart[-1][1]['hint'] and
           restart[-1][1]['guide'] == 'M,Pad.North', 'Restore defaults brings back O and North')
    expect('inputBindings' not in (out / 'restart.player.settings').read_text(encoding='utf-8'), 'restore removes the saved map')

    restored = trace(out / 'restored.trace')
    expect('Dash  O O' in restored[0][1]['hint'] and restored[-1][1]['interact'] == 'True', 'after another restart O O dashes again')


def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    profile = out / 'profile'
    profile.mkdir()
    for name, events in RUNS.items():
        recording(out / f'{name}.inputreplay', events)
        env = dict(os.environ, MONOON_REBIND_TRACE=str(out / f'{name}.trace'), MONOON_USER_DATA=str(profile),
                   SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
        subprocess.run([str(player.resolve()), '--replay-input', str(out / f'{name}.inputreplay'), str(profile)],
                       cwd=player.parent, env=env, timeout=60, check=True)
        shutil.copyfile(profile / 'player.settings', out / f'{name}.player.settings')
    check(out)


if __name__ == '__main__':
    {'prepare': prepare, 'play': play, 'check': check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
