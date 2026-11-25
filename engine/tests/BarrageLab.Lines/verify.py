"""Replay the BarrageLab showcase boss laser (Line 2D) and its comets (Trail 2D) and compare traces and captures of the three
run paths."""
from pathlib import Path
import importlib.util
import math
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
# recording() and compare() are shared with the PlatformerLab path harness.
_spec = importlib.util.spec_from_file_location('paths_verify', HERE.parent / 'PlatformerLab.Paths' / 'verify.py')
paths = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(paths)

PROJECT, SHOWCASE = '6c5dd0cd-638e-5a00-9d80-744596f3a647', 'cc792dff-cd8d-5479-8759-60a69bcd4910'
LASER = '3b1dea80-9a60-5bac-8b16-d6a05f0e7227'
FRAMES = 480
# Space lights the laser at 2, switches it off at 300 (the beam keeps its angle) and on at 360; R at 420 returns every comet
# and restarts the sweep. (Space also stops and restarts the spiral barrage, which the trace does not read.)
EVENTS = [(2, 'Space', 'true'), (3, 'Space', 'false'), (300, 'Space', 'true'), (301, 'Space', 'false'),
          (360, 'Space', 'true'), (361, 'Space', 'false'), (420, 'R', 'true'), (421, 'R', 'false')]
CAPTURES = [60, 200, 330, 425, 470]
REACH, BUDGET = 1400, 40


def recording(path):
    paths.recording(path, EVENTS, FRAMES, PROJECT, SHOWCASE)


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'LineTrace.source', dest / 'Scripts/LineTrace.cs')
    assert f'startScene: {SHOWCASE}' in (dest / 'GameSettings').read_text(encoding='utf-8')
    scene = dest / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '"Comet": {"Id": "d994d3ef-42dc-40d8-8f3c-700a4cf14a1a"}}\n'
    assert text.count(anchor) == 1
    scene.write_text(text.replace(anchor, anchor + f'  - type: script\n    script: LineTrace\n    values: {{"Laser": "{LASER}"}}\n', 1), encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Laser.inputreplay')
    env = dict(os.environ, MONOON_LINE_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'Laser.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    check(out)


def parse(line):
    parts = line.split(' |')
    pts = lambda s: [tuple(map(float, p.split(','))) for p in s.split()]
    comets = []
    for part in parts[2:]:
        head, trail = part.strip().split(' ', 1) if ' ' in part.strip() else (part.strip(), '')
        count, _, rest = trail.partition(':')
        comets.append((tuple(map(float, head.split(','))), int(count), pts(rest)))
    return int(parts[0]), pts(parts[1]), comets


def check(out):
    rows = [parse(line) for line in (out / 'trace.tsv').read_text(encoding='utf-8').splitlines()]
    assert [r[0] for r in rows] == list(range(len(rows))) and len(rows) >= FRAMES - 2, len(rows)
    # The beam is Reach long unless it ends at its third reflection.
    # Rows from frame 4: the laser is lit by Space at frame 2.
    lengths = [sum(math.dist(a, b) for a, b in zip(beam, beam[1:])) for _, beam, _ in rows[4:] if len(beam) < 5]
    bounces = max(len(beam) for _, beam, _ in rows) - 2
    paused = {tuple(rows[i][1]) for i in range(303, 360)}
    trails = [count for _, _, comets in rows for _, count, _ in comets]
    result = {'rows': len(rows), 'beamLength': [round(min(lengths), 3), round(max(lengths), 3)], 'maxBounces': bounces,
              'maxComets': max(len(c) for _, _, c in rows), 'maxTrailPoints': max(trails),
              'cometsAfterRestart': len(rows[424][2])}
    assert all(abs(l - REACH) < .05 for l in lengths), ('laser length is not constant', result)
    assert bounces >= 2 and len(paused) == 1, ('laser does not reflect, or moves while paused', result)
    assert 0 < max(trails) <= BUDGET and result['maxComets'] >= 5, ('trail budget', result)
    assert result['cometsAfterRestart'] <= 1, ('restart returns the comets', result)
    for frame in CAPTURES:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    (out / 'result.json').write_text(str(result) + '\n', encoding='utf-8')
    print(result, flush=True)


if __name__ == '__main__':
    command, *args = sys.argv[1:]
    if command == 'compare':
        paths.CAPTURES[:] = CAPTURES
        paths.compare(*args)
    else: {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in args))
