"""Ride PlatformerLab's path lift through the input actions and compare traces and captures of the three run paths."""
from pathlib import Path
import hashlib
import json
import math
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT, PLATFORM, PATHS = '3b309c28-75ce-562e-92ac-f94ee48b18c7', '4fe9f1eb-5dcd-5f90-a033-78a61e876a0d', '323c83da-1b51-4250-a0c1-fcb85e96f334'
LIFT = '6d66fd60-43c2-4cb3-a0b3-829e88500268'
FRAMES = 400
# Right from frame 10 onto the lift, stand from 86 while it follows its curve, walk onto the goal from 346.
EVENTS = [(10, 'Right', 'true'), (86, 'Right', 'false'), (346, 'Right', 'true')]
CAPTURES = [40, 150, 220, 300, 380]
SPEED = 90 / 60  # px per tick


def recording(path, events=EVENTS, frames=FRAMES, project=PROJECT, start=PATHS):
    lines = [f'- frame: {f}\n  timeTicks: {f * 166667}\n  input: {{kind: key, control: "{k}", down: {d}}}\n' for f, k, d in events]
    path.write_text(f'version: 1\nproject: {project}\nstartScene: {start}\nframeTicks: [' + ', '.join(['166667'] * frames) +
                    ']\nevents:\n' + ''.join(lines), encoding='utf-8')


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'PathTrace.source', dest / 'Scripts/PathTrace.cs')
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert f'startScene: {PLATFORM}' in text
    settings.write_text(text.replace(f'startScene: {PLATFORM}', f'startScene: {PATHS}', 1), encoding='utf-8')
    scene = dest / 'Scenes/Paths.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.count('    script: PathRider\n') == 1
    scene.write_text(text.replace('    script: PathRider\n', f'    script: PathTrace\n    values: {{"Lift": "{LIFT}"}}\n  - type: script\n    script: PathRider\n', 1), encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Ride.inputreplay')
    env = dict(os.environ, MONOON_LINE_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'Ride.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(out)


def check(out):
    rows = [[float(v) for v in line.split(' ')] for line in (out / 'trace.tsv').read_text(encoding='utf-8').splitlines()]
    assert [int(r[0]) for r in rows] == list(range(len(rows))) and len(rows) >= FRAMES - 2, len(rows)
    lift = [(r[3], r[4]) for r in rows]
    first = next(i for i in range(1, len(lift)) if lift[i] != lift[0])
    end = max(range(len(lift)), key=lambda i: lift[i][0])
    # Constant speed: equal arc length per tick between the first moving tick and the end of the path.
    steps = [math.dist(lift[i - 1], lift[i]) for i in range(first + 1, end)]
    ride = [i for i in range(len(rows)) if rows[i][5] == 1]
    standing = [i for i in ride if 90 < i < 345]
    offsets = [(rows[i][1] - rows[i][3], rows[i][2] - rows[i][4]) for i in standing]
    slip = max(math.dist(offsets[0], o) for o in offsets)
    result = {
        'rows': len(rows), 'liftStarts': first, 'pathEnd': end, 'steps': len(steps),
        'stepMin': round(min(steps), 5), 'stepMax': round(max(steps), 5), 'expected': SPEED,
        'apexY': min(y for _, y in lift), 'riderSlip': round(slip, 5), 'runnerFinal': rows[-1][1:3],
    }
    assert all(abs(s - SPEED) <= SPEED * .002 for s in steps), ('lift speed is not constant', result)
    assert slip < .05 and len(standing) > 200, ('the lift does not carry the runner', result)
    assert rows[-1][1] > 600 and abs(rows[-1][2] - 230) < .01, ('runner did not reach the goal ledge', result)
    for frame in CAPTURES:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    (out / 'result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


def compare(*outs):
    outs = [Path(o) for o in outs]
    traces = [(o / 'trace.tsv').read_bytes() for o in outs]
    for o, t in zip(outs[1:], traces[1:]):
        assert t == traces[0], f'{o} trace differs from {outs[0]}'
    rows = traces[0].count(b'\n')
    lines = [f'trace\t{len(traces[0])} bytes\t{rows} rows\tsha256 {hashlib.sha256(traces[0]).hexdigest()[:16]}']
    for frame in CAPTURES:
        hashes = [hashlib.sha256((o / 'captures' / f'{frame:04}.png').read_bytes()).hexdigest() for o in outs]
        assert len(set(hashes)) == 1, f'captures differ at frame {frame}: {hashes}'
        lines.append(f'{frame:04}.png\tidentical\tsha256 {hashes[0][:16]}')
    print('\n'.join(lines), flush=True)
    return lines


if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    if command == 'compare': compare(*paths)
    else: {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
