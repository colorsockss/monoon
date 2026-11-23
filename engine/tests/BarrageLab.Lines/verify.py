"""Replay BarrageLab's laser (Line 2D) and comets (Trail 2D) and compare traces and captures of the three run paths."""
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

PROJECT, START, LASER_SCENE = '6c5dd0cd-638e-5a00-9d80-744596f3a647', '719a1aa5-015b-503e-85ad-0722931cc1c0', '189ac1ae-7fca-4bfd-b5ac-51bcd1090e5f'
LASER = '140e801b-a632-43d1-baca-2f6017f2f1bd'
FRAMES = 480
# Space pauses the sweep at 300 and resumes at 360; R at 420 returns every comet and restarts the pattern.
EVENTS = [(300, 'Space', 'true'), (301, 'Space', 'false'), (360, 'Space', 'true'), (361, 'Space', 'false'),
          (420, 'R', 'true'), (421, 'R', 'false')]
CAPTURES = [60, 200, 330, 425, 470]
REACH, BUDGET = 1400, 40


def recording(path):
    paths.recording(path, EVENTS, FRAMES, PROJECT, LASER_SCENE)


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'LineTrace.source', dest / 'Scripts/LineTrace.cs')
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert f'startScene: {START}' in text
    settings.write_text(text.replace(f'startScene: {START}', f'startScene: {LASER_SCENE}', 1), encoding='utf-8')
    scene = dest / 'Scenes/Laser.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '"Next": {"Id": "719a1aa5-015b-503e-85ad-0722931cc1c0"}}\n'
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
    lengths = [sum(math.dist(a, b) for a, b in zip(beam, beam[1:])) for _, beam, _ in rows[1:] if len(beam) < 5]
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
