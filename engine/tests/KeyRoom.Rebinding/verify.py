"""Replay KeyRoom's controls screen in an exported player and check that a restart keeps the chosen chest key."""
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, START = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7', '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
HINT = '82979000-dcbc-443e-baa1-60ada0400004'
# Centre of Restore defaults on the 800x450 screen: panel (180, 66) + padding and six rows above it.
RESTORE = (270, 348)
FRAMES = 200

# Each run is a new game process on the same profile.
RUNS = {
    'rebind': [(5, 'key', 'K'), (70, 'key', 'Return'), (80, 'key', 'C'),      # swap Interact E <-> Carry coin C
               (90, 'key', 'Return'), (100, 'key', 'Escape'),                  # cancel a keyboard capture
               (106, 'key', 'Right'), (110, 'key', 'Return'), (116, 'pad', 'Pad.North'),  # swap pad South <-> Room guide North
               (130, 'key', 'K'), (150, 'key', 'E')],                          # close, old key must not open the chest
    'restart': [(10, 'key', 'E'), (30, 'key', 'C'),                            # saved keys: E does nothing, C opens
                (50, 'key', 'K'), (120, 'click', RESTORE), (140, 'key', 'K')],
    'restored': [(10, 'key', 'E')],
}


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceBindings.source', destination / 'Scripts/TraceBindings.cs')
    scene = destination / 'Scenes/KeyRoom.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '- id: be0b6e23-58d7-4ed7-a8a4-19dad1c60dc6\n  name: Key bindings opener\n'
    assert text.count(anchor) == 1
    probe = ('- id: 5f1d0c41-7a43-4f0e-9a51-2c7e5d0b7e11\n  name: Rebinding trace\n  width: 1\n  height: 1\n  components:\n'
             f'  - type: script\n    script: TraceBindings\n    values:\n      Hint: "{HINT}"\n')
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
    expect(run[0][1]['hint'].find('Interact: E') >= 0 and run[0][1]['chest'] == 'False', 'first run starts with the project prompt E')
    expect(any(r['capturing'] == 'Interact' for _, r in run), 'Return on the first cell starts capture')
    expect(any(r['interact'] == 'C,Space,Pad.South' and r['carry'] == 'E' and 'Interact: C' in r['hint'] for _, r in run),
           'C swaps with Carry coin and the prompt shows C')
    expect(any(r['interact'] == 'C,Space,Pad.North' and r['guide'] == 'M,Pad.South' and 'Interact: North' in r['hint'] for _, r in run),
           'pad capture swaps North with Room guide and the pad prompt shows North')
    expect(not any(r['interact'] == 'Escape' or 'Escape' in r['interact'] for _, r in run), 'Esc cancels instead of binding')
    expect(run[-1][1]['chest'] == 'False' and 'Interact: C' in run[-1][1]['hint'], 'the old E no longer opens the chest')
    saved = (out / 'rebind.player.settings').read_text(encoding='utf-8')
    expect('inputBindings' in saved and 'Pad.North' in saved and 'CarryCoin' in saved, 'player.settings stores the changed actions')

    restart = trace(out / 'restart.trace')
    first = restart[0][1]
    expect('Interact: C' in first['hint'] and first['interact'] == 'C,Space,Pad.North' and first['carry'] == 'E',
           'restart applies the saved bindings and prompt before any input')
    opened = next(frame for frame, r in restart if r['chest'] == 'True')
    expect(opened > 30, f'E at frame 10 keeps the chest closed; C at frame 30 opens it (frame {opened})')
    expect(restart[-1][1]['interact'] == 'E,Space,Pad.South' and 'Interact: E' in restart[-1][1]['hint'] and
           restart[-1][1]['guide'] == 'M,Pad.North', 'Restore defaults brings back E and North')
    expect('inputBindings' not in (out / 'restart.player.settings').read_text(encoding='utf-8'), 'restore removes the saved map')

    restored = trace(out / 'restored.trace')
    expect('Interact: E' in restored[0][1]['hint'] and restored[-1][1]['chest'] == 'True', 'after another restart E opens the chest again')


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
