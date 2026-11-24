"""Replay the PlatformerLab Slopes scene (capsule runner, tile seams, edge-chain slope) and compare the three run paths."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PLATFORM = '4fe9f1eb-5dcd-5f90-a033-78a61e876a0d'
SLOPES = 'dd0495b5-db6a-4cfe-8b05-bdf45580cdd3'
FRAMES = 450
WALKS = ((12, 199, 1), (202, 389, -1))  # trace rows where Right / Left is held and the runner should move a full step

def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE/'ColliderTrace.source', dest/'Scripts/ColliderTrace.cs')
    settings = dest/'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert f'startScene: {PLATFORM}' in text
    settings.write_text(text.replace(f'startScene: {PLATFORM}', f'startScene: {SLOPES}', 1), encoding='utf-8')
    scene = dest/'Scenes/Slopes.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.count('    script: SlopeRunner\n') == 1
    scene.write_text(text.replace('    script: SlopeRunner\n', '    script: ColliderTrace\n  - type: script\n    script: SlopeRunner\n', 1), encoding='utf-8')

def check(out):
    rows = [line.split(' ') for line in (out/'trace.tsv').read_text(encoding='utf-8').splitlines()]
    assert [int(r[0]) for r in rows] == list(range(len(rows))) and len(rows) >= FRAMES - 2, len(rows)
    x = [float(r[1]) for r in rows]
    y = [float(r[2]) for r in rows]
    # A snag: grounded, walking, but less than half of the 2.5 px step (150 px/s at 60 Hz).
    snags = [i for a, b, sign in WALKS for i in range(a, b) if rows[i][3] == '1' and (x[i + 1] - x[i]) * sign < 1.25]
    boulder = [float(v) for v in rows[-1][4:7]]
    result = {'rows': len(rows), 'startX': x[0], 'farthestX': max(x), 'finalX': x[-1], 'highestY': min(y), 'lowestY': max(y),
              'snags': len(snags), 'boulder': boulder}
    assert not snags, ('runner snagged', snags[:10])
    assert max(x) > 480 and min(y) < 215, ('runner did not climb the edge slope', result)
    assert abs(x[-1] - x[0]) < 1 and max(y) < 270.01, ('runner did not return over the seams', result)
    assert abs(boulder[0] - 710) < 1 and abs(boulder[1] - 204) < .5, ('boulder not resting on the edge chain', boulder)
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
