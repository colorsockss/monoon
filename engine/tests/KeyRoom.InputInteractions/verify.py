"""KeyRoom input interactions and buffering (roadmap 16.12) in a disposable copy, replayed headless.

prepare <sample> <dest>    copy KeyRoom; GameSettings' Hold/Tap/DoubleTap actions (the moves showcase's) are replaced by the
                           harness's, with a buffered action and two 2D vectors (Walk from the existing move actions, Aim from
                           V/B/T/G); a TraceInput observer joins the key room showcase, the start scene
play <MoNoonPlayer> <out>  --replay-input; the trace lists the game tick of every judgement
check <out>...             traces are byte-identical, and the judgements are where the rules put them
"""
import os, shutil, subprocess, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT, START = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7', 'dd96f9dd-668b-5df5-8786-252e6eb77e9e'  # Showcase.scene
FRAMES = 300
HOLD, TAP, DOUBLE, BUFFER = 30, 12, 15, 8
# (down frame, up frame, key): one recording exercises every rule, including the misses.
KEYS = [(10, 50, 'J'), (60, 70, 'J'),                      # Charge: held 40 ticks fires, held 10 ticks does not
        (80, 84, 'U'), (90, 110, 'U'),                     # Flick: 4-tick tap fires on release, 20-tick press does not
        (120, 121, 'O'), (130, 131, 'O'),                  # Dash: second press 10 ticks later fires
        (150, 151, 'O'), (170, 171, 'O'), (180, 181, 'O'), # 20 ticks apart misses, the next one 10 later fires
        (195, 196, 'Y'), (225, 226, 'Y'),                  # Leap: pressed 5 or 15 ticks before a landing (every 20 ticks)
        (240, 270, 'D'), (250, 260, 'V'), (250, 260, 'T')] # Walk right while aiming up-left


def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceInput.source', destination / 'Scripts/TraceInput.cs')
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    anchor = '  - name: KeyBindings\n    controls: [K, Pad.RightShoulder]\n'
    assert text.count(anchor) == 1
    lines = text.splitlines(keepends=True)
    moves = [l for l in lines if l.startswith(('  - {name: Charge,', '  - {name: Flick,', '  - {name: Dash,'))]
    assert len(moves) == 3, moves
    text = ''.join(l for l in lines if l not in moves)
    text = text.replace(anchor, anchor +
        f'  - name: Charge\n    controls: [J]\n    interaction: Hold\n    ticks: {HOLD}\n'
        f'  - name: Flick\n    controls: [U]\n    interaction: Tap\n    ticks: {TAP}\n'
        f'  - name: Dash\n    controls: [O]\n    interaction: DoubleTap\n    ticks: {DOUBLE}\n'
        f'  - name: Leap\n    controls: [Y]\n    buffer: {BUFFER}\n'
        '  - name: AimLeft\n    controls: [V]\n  - name: AimRight\n    controls: [B]\n'
        '  - name: AimUp\n    controls: [T]\n  - name: AimDown\n    controls: [G]\n'
        '  vectors:\n  - {name: Walk, left: MoveLeft, right: MoveRight, up: MoveUp, down: MoveDown}\n'
        '  - {name: Aim, left: AimLeft, right: AimRight, up: AimUp, down: AimDown}\n')
    settings.write_text(text, encoding='utf-8')
    scene = destination / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    assert 'defaultCameraId' not in text   # the probe goes last in the object list
    scene.write_text(text + '- id: 6a2f9c10-4b7e-4d1a-8c3e-2f5b7d9e1a20\n  name: Input trace\n  width: 1\n  height: 1\n  components:\n'
                     '  - type: script\n    script: TraceInput\n', encoding='utf-8')


def recording(path):
    events = []
    for down, up, key in KEYS:
        events += [(down, key, 'true'), (up, key, 'false')]
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{k}", down: {d}}}\n'
                    for f, k, d in sorted(events, key=lambda e: e[0]))
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {START}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents:\n' + lines, encoding='utf-8')


def play(player, out):
    out = out.resolve(); out.mkdir(parents=True)
    recording(out / 'input.inputreplay')
    env = dict(os.environ, MONOON_INPUT_TRACE=str(out / 'input.trace'), MONOON_USER_DATA=str(out / 'profile'),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'play.log').open('wb') as log:
        subprocess.run([str(player.resolve()), '--replay-input', str(out / 'input.inputreplay'), str(out / 'profile')],
                       cwd=player.parent, env=env, stdout=log, stderr=log, timeout=120, check=True)
    check(out)


def expect(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok: raise SystemExit(1)


def check(*outs):
    base = outs[0] / 'input.trace'
    for o in outs[1:]:
        expect((o / 'input.trace').read_bytes() == base.read_bytes(), f'{o.name}: judgement ticks match {outs[0].name} byte for byte')
    events = [(int(t), e) for t, es, _ in (l.split('|') for l in base.read_text(encoding='utf-8').splitlines()) for e in es.split()]
    def ticks(name): return [t for t, e in events if e == name]
    charge, released = ticks('press:Charge'), ticks('release:Charge')
    expect(len(charge) == 2 and ticks('trigger:Charge') == [charge[0] + HOLD - 1],
           f'Hold {HOLD}: fires once on tick {charge[0] + HOLD - 1} of the 40-tick press, not for the 10-tick one ({ticks("trigger:Charge")})')
    flick = ticks('release:Flick')
    expect(ticks('trigger:Flick') == [flick[0]], f'Tap {TAP}: fires on the short release only ({ticks("trigger:Flick")})')
    dash = ticks('press:Dash')
    expect(len(dash) == 5 and ticks('trigger:Dash') == [dash[1], dash[4]], f'DoubleTap {DOUBLE}: second presses 10 ticks apart fire, 20 apart does not ({ticks("trigger:Dash")})')
    leap, land = ticks('press:Leap'), ticks('land:Leap')
    expect(len(leap) == 2 and len(land) == 1 and 0 <= land[0] - leap[0] <= BUFFER and not any(0 <= t - leap[1] <= 20 for t in land),
           f'Buffer {BUFFER}: the press 5 ticks before a landing is consumed there ({land}), the one 15 ticks before expires')
    held = [t for t, e in events if e == 'trigger:Leap']
    expect(held == list(range(leap[0], land[0] + 1)) + list(range(leap[1], leap[1] + BUFFER + 1)),
           'Triggered stays true for the buffer window and stops once consumed')
    vectors = [l.split('|')[2] for l in base.read_text(encoding='utf-8').splitlines()]
    expect(any(v.startswith('walk=1,0 aim=-0.70710677,-0.70710677 move=1,0') for v in vectors), 'Walk and Aim vectors; Move is the first vector')


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
