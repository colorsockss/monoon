"""SeedLab terrain costs and cell edits on the showcase route map: a disposable copy swaps the showcase RouteCourier for the
key driver DriveRoute (../SeedLab.GridNavigation), replays seven cases and checks the drawn cells, terrain and costs, tile
collision and the courier's routes against the independent model in route_model.py."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'SeedLab.GridNavigation'))
import route_model as m

HERE = Path(__file__).resolve().parent
FRAMES = 360
# (name, directions, south bridge, rain on the north road, key pressed while walking or None)
CASES = [
    ('four-dry', 4, False, False, None),
    ('four-rain', 4, False, True, None),
    ('four-bridge-rain', 4, True, True, None),
    ('eight-rain', 8, False, True, None),
    ('eight-bridge', 8, True, False, None),
    ('four-live-rain', 4, False, False, 'B'),
    ('four-live-bridge', 4, False, True, 'O'),
]
LIVE = {'B': 'rain', 'O': 'bridge'}


def prepare(sample, destination):
    m.prepare(sample, destination, HERE / 'TraceTerrain.source')


def recording(path):
    m.recording(path, m.plan(CASES), FRAMES)


def tileset_terms(path):
    """Minimal reader for the flat Field.tileset: id -> (solid, terrain, moveCost)."""
    tiles = {}
    for block in path.read_text(encoding='utf-8').split('\n- id: ')[1:]:
        tid = int(block.split('\n')[0])
        field = lambda k: (re.search(rf'^  {k}: (.+)$', block, re.M) or [None, None])[1]
        tiles[tid] = (field('solid') == 'true', field('terrain'), int(field('moveCost')) if field('moveCost') else None)
    return tiles


def expected(s):
    """display:terrain:cost per cell; blocked tiles report cost 0."""
    return [f'{m.tile((x, y), s)}:{m.terrain((x, y), s)}:{m.weight((x, y), s)}' for y in range(m.H) for x in range(m.W)]


def compare(name, label, snap, s):
    _, _, cells, hits = snap
    assert cells == expected(s), (name, label, [(i % m.W, i // m.W, a, b) for i, (a, b) in enumerate(zip(cells, expected(s))) if a != b][:4])
    assert hits == [0 if s[1] else 1, 1, 0, 0], (name, label, hits)


def check(out, tileset=None):
    if tileset:
        t = tileset_terms(Path(tileset))
        assert t[2] == (False, 'road', 1) and t[3] == (False, 'mud', 8) and t[5] == (False, 'bridge', 1) and t[1] == (False, 'grass', 3) \
            and t[4] == (True, 'water', None) and t[6] == (True, 'hedge', None), t
    cases = []
    for line in (out / 'trace.txt').read_text(encoding='utf-8').splitlines():
        parts = line.split()
        if parts[0] == 'state':
            if cases: cases[-1][1].append((len(cases[-1][0]), parts[1], parts[2].split(','), [int(v) for v in parts[3:]]))
            continue
        action, x, y = parts
        if action == 'reset': cases.append(([], []))
        if cases: cases[-1][0].append((action, m.cell_of(x, y)))
    steps = m.plan(CASES)
    assert len(cases) == len(steps), len(cases)
    summary = {}
    for (name, s, _, live, final), (records, snaps) in zip(steps, cases):
        assert records[0] == ('reset', m.START) and records[-1][1] == m.GOAL, (name, records)
        live_action = LIVE.get(live)
        route, cost, pivot, setting, walking = [m.START], 0, None, s, False
        for action, p in records[1:]:
            walking |= action == 'walk'
            if live_action and walking and action == live_action:
                pivot = route[-1]
                assert pivot not in (m.START, m.GOAL) and p == pivot, (name, route, p)
                setting = final
            if p != route[-1]:
                cost += dict(m.neighbors(route[-1], setting))[p]
                route.append(p)
        if live:
            assert pivot is not None, name
            suffix = route[route.index(pivot):]
            assert sum(dict(m.neighbors(a, final))[b] for a, b in zip(suffix, suffix[1:])) == m.minimum(pivot, final), (name, suffix)
        else:
            assert cost == m.minimum(m.START, s), (name, cost, m.minimum(m.START, s))
        # Drawn cells, terrain, cost and tile collision the tick after the walk starts, and after a live change.
        walk = [x for x in snaps if x[1] == 'walk']
        assert len(walk) == 1, (name, snaps)
        compare(name, 'walk', walk[0], s)
        if live:
            changed = [x for x in snaps[snaps.index(walk[0]):] if x[1] == live_action]
            assert len(changed) == 1, (name, snaps)
            compare(name, 'live', changed[0], final)
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
