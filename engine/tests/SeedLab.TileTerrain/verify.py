"""Replay SeedLab terrain edits and check drawn cells, tile collision and courier routes against an independent model."""
import heapq
import itertools
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
W, H = 13, 7
START, GOAL, GATE = (1, 4), (11, 4), (6, 4)
MUD = {(x, 5) for x in range(3, 10)}
PATCH = {(x, 1) for x in range(4, 9)}
FLOOR = {(x, y) for y, spans in enumerate([[], [(1, 11)], [(1, 2), (10, 11)],
         [(1, 3), (9, 11)], [(1, 11)], [(1, 11)]]) for lo, hi in spans for x in range(lo, hi + 1)}
# Reference weights, written independently of Route.tileset (check() also compares them with the asset).
MUD_COST, DRY_COST = 12, 1
# (name, directions, door open, north painted, dry mud, key pressed while walking or None)
CASES = [
    ('four-closed', 4, False, False, False, None),
    ('four-painted', 4, False, True, False, None),
    ('four-open-painted', 4, True, True, False, None),
    ('eight-painted', 8, False, True, False, None),
    ('eight-dry-mud', 8, False, True, True, None),
    ('four-live-paint', 4, False, False, False, 'B'),
    ('four-live-door', 4, False, True, False, 'O'),
]
KEYS = {'V': 1, 'O': 2, 'B': 3, 'M': 4}
FRAMES = 235

def prepare(sample, destination):
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'TraceTerrain.source', destination / 'Scripts/TraceTerrain.cs')
    scene = destination / 'Scenes/Route.scene'
    text = scene.read_text(encoding='utf-8')
    anchor = '  - {type: spriteRenderer, squareColor: "#FFF2CF", order: 2}'
    assert text.count(anchor) == 1
    scene.write_text(text.replace(anchor, anchor + '\n  - {type: script, script: TraceTerrain}'), encoding='utf-8')

def after(setting, key):
    s = list(setting)
    s[KEYS[key]] = (8 if s[1] == 4 else 4) if key == 'V' else not s[KEYS[key]]
    return tuple(s)

def plan():
    """Per case: settings before walking, toggles to get there, and settings after a live key."""
    current, steps = ('', 4, False, False, False), []
    for name, *setting, live in CASES:
        target = ('', *setting)
        toggles = [k for k, i in KEYS.items() if current[i] != target[i]]
        final = after(target, live) if live else target
        steps.append((name, target, toggles, live, final))
        current = final
    return steps

def recording(path):
    events = []
    def press(frame, key):
        for offset, down in [(0, 'true'), (1, 'false')]:
            tick = frame + offset
            events.append(f'- frame: {tick}\n  timeTicks: {tick * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n')
    press(2, 'G')
    for index, (name, _, toggles, live, _) in enumerate(plan()):
        base = 10 + index * FRAMES
        press(base, 'R')
        for offset, key in enumerate(toggles): press(base + 3 + 3 * offset, key)
        press(base + 15, 'Space')
        if live: press(base + 43, live)
    path.write_text('version: 1\nproject: f308a024-f67d-5f58-a81c-f501e0b42828\n'
                    'startScene: 0cfbf6ac-2359-53aa-abc6-f603bdf4dbee\nframeTicks: [' +
                    ', '.join(['166667'] * (10 + len(CASES) * FRAMES)) + ']\nevents:\n' + ''.join(events), encoding='utf-8')

def weight(p, s):
    _, _, door, painted, dry = s
    if p not in FLOOR or p == GATE and not door: return 0
    return (DRY_COST if dry else MUD_COST) if p in MUD or painted and p in PATCH else 1

def neighbors(cell, s):
    x, y = cell
    for dx, dy in itertools.product((-1, 0, 1), repeat=2):
        p = x + dx, y + dy
        if p == cell or not weight(p, s): continue
        diagonal = dx != 0 and dy != 0
        if diagonal and (s[1] == 4 or not (weight((p[0], y), s) and weight((x, p[1]), s))): continue
        yield p, (14 if diagonal else 10) * weight(p, s)

def minimum(start, s):
    queue, visited = [(0, start)], set()
    while queue:
        cost, p = heapq.heappop(queue)
        if p == GOAL: return cost
        if p in visited: continue
        visited.add(p)
        for n, step in neighbors(p, s): heapq.heappush(queue, (cost + step, n))
    raise AssertionError('Goal unreachable in reference map')

def board(s, dots):
    """Expected drawn tile per cell: Route.tileset IDs, with dotted variants on planned cells."""
    _, _, door, painted, dry = s
    mud = 10 if dry else 3
    def tile(p):
        if p not in FLOOR: return 1
        if p == GATE: return 5 if door else 4
        if p == START: return 6
        if p == GOAL: return 7
        base = mud if p in MUD or painted and p in PATCH else 2
        return {2: 8, 3: 9, 10: 11}[base] if p in dots else base
    return [tile((x, y)) for y in range(H) for x in range(W)]

def tileset_terms(path):
    """Minimal reader for the flat Route.tileset: id -> (solid, terrain, moveCost)."""
    tiles = {}
    for block in path.read_text(encoding='utf-8').split('\n- id: ')[1:]:
        tid = int(block.split('\n')[0])
        field = lambda k: (re.search(rf'^  {k}: (.+)$', block, re.M) or [None, None])[1]
        tiles[tid] = (field('solid') == 'true', field('terrain'), int(field('moveCost')) if field('moveCost') else None)
    return tiles

def check(out, tileset=None):
    if tileset:
        t = tileset_terms(Path(tileset))
        assert t[3] == (False, 'mud', MUD_COST) and t[10] == (False, 'dry mud', DRY_COST) and t[4][:2] == (True, 'door') \
            and t[4][2] is None and t[5] == (False, 'door', 1) and t[1][0], t
    cases, states = [], []
    for line in (out / 'trace.txt').read_text(encoding='utf-8').splitlines():
        parts = line.split()
        if parts[0] == 'state':
            if cases: cases[-1][1].append((len(cases[-1][0]), parts[1], parts[2].split(','), [int(v) for v in parts[3:]]))
            continue
        action, x, y = parts
        px, py = int(x) - 12, int(y) - 12
        assert px % 40 == py % 40 == 0, line
        if action == 'reset': cases.append(([], []))
        if cases: cases[-1][0].append((action, (px // 40, py // 40)))
    steps = plan()
    assert len(cases) == len(steps), len(cases)
    summary = {}
    for (name, s, toggles, live, final), (records, snaps) in zip(steps, cases):
        assert records[0] == ('reset', START) and records[-1][1] == GOAL, (name, records)
        live_action = {'B': 'paint', 'O': 'gate'}.get(live)
        route, cost, pivot, setting, walking = [START], 0, None, s, False
        for action, p in records[1:]:
            walking |= action == 'walk'
            if live_action and walking and action == live_action:
                pivot = route[-1]
                assert pivot not in (START, GOAL, GATE) and p == pivot, (name, route, p)
                setting = final
            if p != route[-1]:
                cost += dict(neighbors(route[-1], setting))[p]
                route.append(p)
        if live:
            assert pivot is not None, name
            suffix = route[route.index(pivot):]
            assert sum(dict(neighbors(a, final))[b] for a, b in zip(suffix, suffix[1:])) == minimum(pivot, final), (name, suffix)
        else:
            assert cost == minimum(START, s), (name, cost, minimum(START, s))
        # Drawn cells, terrain and tile collision the tick after an input. Dots must mark a cheapest plan;
        # without a live change the courier then walks exactly the dotted cells.
        walk = [x for x in snaps if x[1] == 'walk']
        assert len(walk) == 1, (name, snaps)
        walk_index = snaps.index(walk[0])
        def compare(snap, setting, start, label):
            _, _, cells, hits = snap
            drawn = [int(c.split(':')[0]) for c in cells]
            dots = {(i % W, i // W) for i, t in enumerate(drawn) if t in (8, 9, 11)}
            assert drawn == board(setting, dots), (name, label, drawn, board(setting, dots))
            usable = dots | {start, GOAL} | ({GATE} if setting[2] else set())
            restricted = lambda p, q: q in usable
            queue, best = [(0, start)], {}
            while queue:
                c, p = heapq.heappop(queue)
                if p in best: continue
                best[p] = c
                for n, step in neighbors(p, setting):
                    if restricted(p, n): heapq.heappush(queue, (c + step, n))
            assert best.get(GOAL) == minimum(start, setting), (name, label, sorted(dots))
            door = cells[GATE[1] * W + GATE[0]].split(':')
            assert door == (['5', 'door', '1'] if setting[2] else ['4', 'door', '0']), (name, label, door)
            for p in PATCH | MUD:
                terrain, move = cells[p[1] * W + p[0]].split(':')[1:]
                muddy = p in MUD or setting[3] and p in PATCH
                assert (terrain, int(move)) == ((('dry_mud', DRY_COST) if setting[4] else ('mud', MUD_COST)) if muddy else ('floor', 1)), (name, label, p)
            assert hits == [0 if setting[2] else 1, 1, 0, 0], (name, label, hits)
            return usable
        walked = set(route if not live else route[:route.index(pivot) + 1])
        shown = compare(walk[0], s, START, 'walk')
        assert walked <= shown and (live or shown - {GATE} == walked - {GATE}), (name, sorted(shown), route)
        if live:
            replanned = [x for x in snaps[walk_index:] if x[1] == live_action]
            assert len(replanned) == 1, (name, snaps)
            suffix = set(route[route.index(pivot):])
            assert compare(replanned[0], final, pivot, 'live') - {GATE} == suffix - {GATE}, (name, suffix)
        summary[name] = {'steps': len(route) - 1, 'cost': cost, 'cells': route}
        if pivot: summary[name]['changedAt'] = pivot
    (out / 'result.json').write_bytes((json.dumps(summary, separators=(',', ':')) + '\n').encode())
    print({name: (r['steps'], r['cost']) for name, r in summary.items()}, flush=True)

def play(player, out, tileset=None):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Terrain.inputreplay')
    env = dict(os.environ, MONOON_ROUTE_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(player.resolve()), '--replay-input', str(out / 'Terrain.inputreplay'), str(out / 'slot')], env=env, check=True)
    check(out, tileset)

if __name__ == '__main__':
    {'prepare': prepare, 'play': play, 'check': check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
