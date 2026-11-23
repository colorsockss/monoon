"""Replay the Joints scene through its input actions and compare the Box2D position traces of the three run paths."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PLATFORM = '4fe9f1eb-5dcd-5f90-a033-78a61e876a0d'
JOINTS = '4fbf8054-5a0b-5822-86eb-a809b0f6c0dd'
FRAMES = 300

def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE/'JointTrace.source', dest/'Scripts/JointTrace.cs')
    settings = dest/'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert f'startScene: {PLATFORM}' in text
    settings.write_text(text.replace(f'startScene: {PLATFORM}', f'startScene: {JOINTS}', 1), encoding='utf-8')
    scene = dest/'Scenes/Joints.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.count('    script: Crosser\n') == 1
    scene.write_text(text.replace('    script: Crosser\n', '    script: JointTrace\n  - type: script\n    script: Crosser\n', 1), encoding='utf-8')

def check(out):
    rows = [line.split(' ') for line in (out/'trace.tsv').read_text(encoding='utf-8').splitlines()]
    assert [int(r[0]) for r in rows] == list(range(len(rows))) and len(rows) >= FRAMES - 2, len(rows)
    assert all(len(r) == 3 + 10 * 3 for r in rows), len(rows[0])
    x = [float(r[1]) for r in rows]
    planks = [[float(v) for v in r[3:27]] for r in rows]
    decks = [(float(r[28]), float(r[31])) for r in rows]
    lowest = max(max(p[1::3]) for p in planks)
    a0, b0 = decks[0]
    result = {
        'rows': len(rows),
        'runnerFinalX': x[-1],
        'lowestPlankY': lowest,
        'deckATravel': max(a - a0 for a, _ in decks),
        'deckBTravel': min(b - b0 for _, b in decks),
    }
    assert x[-1] > 690, ('runner did not reach the goal ledge', x[-1])
    assert lowest > 300, ('bridge did not sag', lowest)
    assert result['deckATravel'] > 10 and result['deckBTravel'] < -10, result
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
    env = dict(os.environ, MONOON_JOINT_TRACE=str((out/'trace.tsv').resolve()), MONOON_USER_DATA=str((out/'profile').resolve()))
    subprocess.run([str(exe.resolve()), '--replay-input', str(HERE/'Cross.inputreplay'), str((out/'slot').resolve())], env=env, check=True)
    check(out)

if __name__ == '__main__':
    command, *paths = sys.argv[1:]
    if command == 'compare': compare(*paths)
    else: {'prepare': prepare, 'play': play, 'check': check}[command](*(Path(p) for p in paths))
