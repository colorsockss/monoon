"""Copy SeedLab with a crowd trace, replay the same input in a player or the editor, and check the 200 couriers."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
FRAMES = 1300
GATE_FRAME = 130
GOALS = [(665 + 10 * c, 155 + 10 * r) for r in range(20) for c in range(10)]
START = 'startScene: cdfa4adc-9fd0-58dc-bdff-271497eca1f0'  # ShowcaseMaze, the sample's start scene
CROWD = '578c4bc5-e685-5420-b1ac-42e255eb5bea'  # Scenes/ShowcaseCrowd.scene

def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceCrowd.source', destination / 'Scripts/CrowdRun.Trace.cs')
    # The showcase scenes stand alone, so the copy starts in the crowd yard.
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert text.count(START) == 1
    settings.write_text(text.replace(START, f'startScene: {CROWD}'), encoding='utf-8')

def recording(path):
    events = []
    def press(frame, key):
        for offset, down in [(0, 'true'), (1, 'false')]:
            tick = frame + offset
            events.append(f'- frame: {tick}\n  timeTicks: {tick * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n')
    press(GATE_FRAME, 'O')  # open the gate while the crowd walks: every courier re-plans
    path.write_text('version: 1\nproject: f308a024-f67d-5f58-a81c-f501e0b42828\n'
                    f'startScene: {CROWD}\nframeTicks: [' +
                    ', '.join(['166667'] * FRAMES) + ']\nevents:\n' + ''.join(events), encoding='utf-8')

def check(out):
    lines = (out / 'trace.txt').read_text(encoding='utf-8').splitlines()
    assert not any(l.startswith('inside') for l in lines), [l for l in lines if l.startswith('inside')][:3]
    gates = [l for l in lines if l.startswith('gate')]
    assert len(gates) == 1, gates
    done = [i for i, l in enumerate(lines) if l.startswith('done')]
    assert len(done) == 1, 'not every courier arrived: ' + (lines[-1] if lines else 'empty trace')
    _, ticks, final_hash = lines[done[0]].split()
    positions = [tuple(float(v) for v in l.split()) for l in lines[done[0] + 1:]]
    assert positions == [(float(x), float(y)) for x, y in GOALS], 'couriers are not on their own goals'
    digest = hashlib.sha256((out / 'trace.txt').read_bytes()).hexdigest()
    summary = {'arrivedTick': int(ticks), 'gateOpenedTick': int(gates[0].split()[1]), 'positionHash': final_hash,
               'traceLines': len(lines), 'traceSha256': digest}
    (out / 'result.json').write_text(json.dumps(summary, separators=(',', ':')) + '\n', encoding='utf-8')
    print(summary, flush=True)

def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Crowd.inputreplay')
    env = dict(os.environ, MONOON_CROWD_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(player.resolve()), '--replay-input', str(out / 'Crowd.inputreplay'), str(out / 'slot')], env=env, check=True)
    check(out)

def compare(*outs):
    results = [json.loads((o / 'result.json').read_text(encoding='utf-8')) for o in outs]
    assert all(r == results[0] for r in results), results
    print('identical traces:', results[0]['traceSha256'])

if __name__ == '__main__':
    command, args = sys.argv[1], [Path(p) for p in sys.argv[2:]]
    {'prepare': prepare, 'play': play, 'check': check, 'compare': compare}[command](*args)
