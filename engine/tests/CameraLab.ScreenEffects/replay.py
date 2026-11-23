"""Headless variant of verify.py for desktops without the Mac window tool and rumble fixture (Linux and Windows guests).

The same key sequence as verify.py's first run and restart goes through each exported player's --replay-input entry point
(keyboard controls of the same actions instead of the virtual pad). Compared: the effect record (slot 3), player.settings
after each run and the frozen frames drawn by the player's SDL canvas. Rumble reaching a pad, the custom cursor and the
editor's Scene Play are not covered here (no window, no pad); verify.py checks those on the Mac.

Usage: replay.py play <MoNoonPlayer> <out>      replay.py compare <out> <out> ...
"""
import hashlib, json, os, subprocess, sys
from pathlib import Path

PROJECT = 'cf624488-489b-5951-a279-fffa22d99f7a'
KEYS = {'.': 'Period', 'F': 'F'}


def script(run):
    """(events, frames, snapshots) for verify.py's run() ('first') or its restart ('restart'). One press = down, up, one idle frame."""
    events, snaps, frame = [], {}, [5]
    def key(k, times=1):
        for _ in range(times):
            events.append((frame[0], KEYS.get(k, k), True)); events.append((frame[0] + 1, KEYS.get(k, k), False)); frame[0] += 3
    def wait(seconds): frame[0] += round(seconds * 60)
    def snap(name): snaps[frame[0]] = name; frame[0] += 1
    def frozen(trigger, steps, name, finish): key(trigger); key('.', steps); snap(name); key('.', finish)
    if run == 'first':
        snap('off'); key('F'); snap('still')
        frozen('1', 6, 'shake', 30); frozen('2', 6, 'flash', 30); frozen('4', 24, 'grade', 6); frozen('3', 15, 'fade', 30)
        key('3'); key('.', 36); key('4'); key('.', 24)
        key('5')
        frozen('1', 6, 'shake-reduced', 30); frozen('2', 6, 'flash-reduced', 30)
        key('F'); wait(.3)
        key('6'); wait(.8); key('1'); wait(1); key('8'); key('6'); wait(.5); key('1'); wait(1)
        key('7'); wait(.5); key('7'); wait(.5)
    else:
        snap('restart'); key('6'); wait(.6); key('8'); key('6'); wait(.8)
    return events, frame[0] + 10, snaps


def recording(path, run):
    events, frames, snaps = script(run)
    lines = ''.join(f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{c}", down: {str(d).lower()}}}\n' for f, c, d in events)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {PROJECT}\nframeTicks: [' + ', '.join(['166667'] * frames) +
                    ']\nevents:\n' + lines, encoding='utf-8')
    return snaps


def expect(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok: raise SystemExit(1)


def found(profile, name):
    hits = sorted(profile.rglob(name))
    assert len(hits) == 1, (name, hits)
    return hits[0]


def play(player, out):
    out = out.resolve(); out.mkdir(parents=True)
    profile = out / 'profile'
    for run in ('first', 'restart'):
        snaps = recording(out / f'{run}.inputreplay', run)
        (out / f'{run}.snapshots.json').write_text(json.dumps(snaps) + '\n', encoding='utf-8')
        env = dict(os.environ, MONOON_USER_DATA=str(profile), MONOON_FEEDBACK_LOG=str(out / 'feedback.log'),
                   MONOON_REPLAY_SNAPSHOTS=f'{out / run};{",".join(map(str, snaps))}',
                   SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
        with (out / f'{run}.log').open('wb') as log:
            subprocess.run([str(player.resolve()), '--replay-input', str(out / f'{run}.inputreplay'), str(profile)],
                           env=env, stdout=log, stderr=log, timeout=300, check=True)
        (out / f'{run}.player.settings').write_bytes(found(profile, 'player.settings').read_bytes())
        if run == 'first': (out / 'record').write_bytes(found(profile, 'slot-003.save').read_bytes())
    check(out)


def check(out):
    record, first, restart = (out / 'record').read_text(), (out / 'first.player.settings').read_text(), (out / 'restart.player.settings').read_text()
    for key in ('impact', 'flash', 'grade', 'fadeOut', 'fadeIn', 'ungrade', 'impactReduced', 'flashReduced', 'buzzReduced'):
        expect(all(f'{key}.{n}:' in record for n in (1, 6, 12, 24)), f'record has {key} on ticks 1, 6, 12, 24')
    expect('accessibility.shakeScale: 0.25' in first and 'accessibility.flashScale: 0.25' in first and 'accessibility.rumble: false' in first,
           'player.settings stores reduced shake/flash and rumble off')
    expect('accessibility.rumble: true' in restart and 'accessibility.shakeScale: 0.25' in restart, 'restart keeps reduced comfort, then rumble on')
    for run in ('first', 'restart'):
        for frame in json.loads((out / f'{run}.snapshots.json').read_text()):
            expect((out / run / f'{int(frame):04}.png').stat().st_size > 1000, f'{run} frame {frame} drawn')


def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()


def compare(*outs):
    base = outs[0]
    for name in ('record', 'first.player.settings', 'restart.player.settings'):
        for o in outs[1:]:
            expect((o / name).read_bytes() == (base / name).read_bytes(), f'{o.name}: {name} matches {base.name} byte for byte')
    for run in ('first', 'restart'):
        snaps = json.loads((base / f'{run}.snapshots.json').read_text())
        for frame, label in snaps.items():
            hashes = [digest(o / run / f'{int(frame):04}.png') for o in outs]
            print(f'{run} {frame} {label}: ' + ' '.join(f'{o.name}={h[:12]}' for o, h in zip(outs, hashes)), flush=True)


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    {'play': play, 'compare': compare, 'check': check}[command](*(Path(p) for p in paths))
