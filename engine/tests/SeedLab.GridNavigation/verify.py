"""Build a disposable observer fixture and verify the tiles its courier actually visits."""
import heapq
import itertools
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
START, GOAL, GATE = (1, 4), (11, 4), (6, 4)
MUD = {(x, 5) for x in range(3, 10)}
# Independent description of the walkable rows in the authored sample.
FLOOR = {(x, y) for y, spans in enumerate([[], [(1, 11)], [(1, 2), (10, 11)],
         [(1, 3), (9, 11)], [(1, 11)], [(1, 11)]]) for lo, hi in spans for x in range(lo, hi + 1)}
# Inputs toggle the previous case's settings. Each R returns the courier to the start.
CASES = [
    ('four-closed', 4, False, 12, []),
    ('four-open', 4, True, 12, ['O']),
    ('eight-closed', 8, False, 12, ['V', 'O']),
    ('eight-open', 8, True, 12, ['O']),
    ('eight-cheap-mud', 8, False, 1, ['O', 'M']),
    ('four-live-open', 4, False, 12, ['V', 'M']),
]

def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceCourier.source', destination / 'Scripts/TraceCourier.cs')
    scene = destination / 'Scenes/Route.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '  - {type: spriteRenderer, squareColor: "#FFF2CF", order: 2}'
    assert text.count(anchor) == 1
    scene.write_text(text.replace(anchor, anchor + '\n  - {type: script, script: TraceCourier}'), encoding='utf-8')

def recording(path):
    events = []
    def press(frame, key):
        for offset, down in [(0, 'true'), (1, 'false')]:
            tick = frame + offset
            events.append(f'- frame: {tick}\n  timeTicks: {tick * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n')
    press(2, 'G')
    for index, (name, _, _, _, toggles) in enumerate(CASES):
        base = 10 + index * 235
        press(base, 'R')
        for offset, key in enumerate(toggles): press(base + 3 + 3 * offset, key)
        press(base + 12, 'Space')
        if name == 'four-live-open': press(base + 40, 'O')
    path.write_text('version: 1\nproject: f308a024-f67d-5f58-a81c-f501e0b42828\n'
                    'startScene: 0cfbf6ac-2359-53aa-abc6-f603bdf4dbee\nframeTicks: [' +
                    ', '.join(['166667'] * (10 + len(CASES) * 235)) + ']\nevents:\n' + ''.join(events), encoding='utf-8')

def neighbors(cell, directions, door, mud):
    x, y = cell
    def walk(p): return p in FLOOR and (door or p != GATE)
    for dx, dy in itertools.product((-1, 0, 1), repeat=2):
        p = x + dx, y + dy
        if p == cell or not walk(p): continue
        diagonal = dx != 0 and dy != 0
        if diagonal and (directions == 4 or not (walk((p[0], y)) and walk((x, p[1])))): continue
        yield p, (14 if diagonal else 10) * (mud if p in MUD else 1)

def minimum(start, directions, door, mud):
    queue = [(0, start)]
    visited = set()
    while queue:
        cost, p = heapq.heappop(queue)
        if p == GOAL: return cost
        if p in visited: continue
        visited.add(p)
        for n, step in neighbors(p, directions, door, mud): heapq.heappush(queue, (cost + step, n))
    raise AssertionError('Goal unreachable in reference map')

def check(out):
    cases = []
    for line in (out / 'trace.txt').read_text(encoding='utf-8').splitlines():
        action, x, y = line.split()
        px, py = int(x) - 12, int(y) - 12
        assert px % 40 == py % 40 == 0, line
        if action == 'reset': cases.append([])
        if cases: cases[-1].append((action, (px // 40, py // 40)))
    assert len(cases) == len(CASES), cases
    summary = {}
    for (name, directions, door, mud, toggles), records in zip(CASES, cases):
        assert records[0] == ('reset', START) and records[-1][1] == GOAL, (name, records)
        live = name == 'four-live-open'
        route, cost, pivot = [START], 0, None
        for action, p in records[1:]:
            if live and action == 'gate':
                pivot = route[-1]
                assert pivot not in (START, GOAL, GATE) and p == pivot, (name, route, p)
                door = True
            if p != route[-1]:
                cost += dict(neighbors(route[-1], directions, door, mud))[p]
                route.append(p)
        assert (GATE in route) == door and bool(set(route) & MUD) == (mud == 1), (name, route)
        if directions == 8 and not door:
            assert any(a[0] != b[0] and a[1] != b[1] for a, b in zip(route, route[1:])), route
        if live:
            assert pivot is not None
            suffix = route[route.index(pivot):]
            suffix_cost = sum(dict(neighbors(a, directions, True, mud))[b] for a,b in zip(suffix,suffix[1:]))
            assert suffix_cost == minimum(pivot, directions, True, mud)
        else:
            assert cost == minimum(START, directions, door, mud), (name, cost)
        summary[name] = {'steps':len(route) - 1, 'cost':cost, 'cells':route}
        if pivot: summary[name]['doorOpenedAt'] = pivot
    (out / 'result.json').write_bytes((json.dumps(summary, separators=(',', ':')) + '\n').encode())
    print({name:(r['steps'],r['cost']) for name,r in summary.items()}, flush=True)

def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Routes.inputreplay')
    env = dict(os.environ, MONOON_ROUTE_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(player.resolve()), '--replay-input', str(out / 'Routes.inputreplay'), str(out / 'slot')], env=env, check=True)
    check(out)

if __name__ == '__main__':
    {'prepare':prepare, 'play':play, 'check':check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
