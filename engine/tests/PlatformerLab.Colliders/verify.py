"""Run PlatformerLab's course showcase (ShowcaseCourse: CourseRunner walks its capsule over the Hill edge chain to the pennant,
no input; RoundTrip.inputreplay is 1200 frames with no events) and compare the three run paths."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
SHOWCASE = '40845ca7-0281-5a45-878a-c1da3b04f822'
COURSE = 'e4366c3e-3cfa-5c9b-b11e-7e2f25ffb84f'
FRAMES = 1200
HILL = (1600, 2080)   # the Hill edge chain's x range; its top is at y 160


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE/'ColliderTrace.source', dest/'Scripts/ColliderTrace.cs')
    settings = dest/'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert f'startScene: {SHOWCASE}' in text
    settings.write_text(text.replace(f'startScene: {SHOWCASE}', f'startScene: {COURSE}', 1), encoding='utf-8')
    scene = dest/'Scenes/ShowcaseCourse.scene'
    text = scene.read_text(encoding='utf-8')
    runner = '    script: Showcase/CourseRunner\n'
    assert text.count(runner) == 1
    scene.write_text(text.replace(runner, '    script: ColliderTrace\n  - type: script\n' + runner, 1), encoding='utf-8')


def check(out):
    rows = [line.split(' ') for line in (out/'trace.tsv').read_text(encoding='utf-8').splitlines()]
    assert [int(r[0]) for r in rows] == list(range(len(rows))) and len(rows) >= FRAMES - 2, len(rows)
    x = [float(r[1]) for r in rows]
    y = [float(r[2]) for r in rows]
    hill = [i for i in range(len(rows) - 1) if HILL[0] + 20 < x[i] < HILL[1] - 40 and rows[i][3] == '1']
    # A snag: grounded on the chain but less than half of the 2.5 unit step (150 units/s at 60 Hz).
    snags = [i for i in hill if x[i + 1] - x[i] < 1.25]
    result = {'rows': len(rows), 'startX': x[0], 'finalX': x[-1], 'highestY': min(y), 'hillTicks': len(hill), 'snags': len(snags)}
    assert len(hill) > 120 and not snags, ('capsule snagged on the edge chain', result, snags[:10])
    assert min(y[i] for i in hill) < 170, ('runner did not climb the hill', result)
    assert x[-1] > 2200, ('runner did not reach the pennant', result)
    (out/'result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


def compare(*outs):
    traces = [(Path(o)/'trace.tsv').read_bytes() for o in outs]
    for o, t in zip(outs[1:], traces[1:]):
        assert t == traces[0], f'{o} differs from {outs[0]}'
    rows = traces[0].count(b'\n')
    print(f'{len(outs)} traces identical: {len(traces[0])} bytes, {rows} rows', flush=True)

def play(exe, out):
    out.mkdir(parents=True)
    env = dict(os.environ, MONOON_COLLIDER_TRACE=str((out/'trace.tsv').resolve()), MONOON_USER_DATA=str((out/'profile').resolve()))
    subprocess.run([str(exe.resolve()), '--replay-input', str(HERE/'RoundTrip.inputreplay'), str((out/'slot').resolve())], env=env, check=True)
    check(out)

if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    if command == 'compare': compare(*paths)
    else: {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
