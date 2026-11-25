"""SeedLab grid navigation on the showcase route map: a disposable copy swaps the showcase RouteCourier for the key driver
DriveRoute, replays six cases and checks the cells the courier actually visits against an independent Dijkstra
(route_model.py)."""
import json
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import route_model as m

FRAMES = 330
# (name, directions, south bridge, rain on the north road, key pressed while walking or None)
CASES = [
    ('four-dry', 4, False, False, None),
    ('four-bridge', 4, True, False, None),
    ('eight-bridge', 8, True, False, None),
    ('eight-dry', 8, False, False, None),
    ('eight-rain', 8, False, True, None),
    ('four-live-bridge', 4, False, False, 'O'),
]


def prepare(sample, destination):
    m.prepare(sample, destination, m.HERE / 'TraceCourier.source')


def recording(path):
    m.recording(path, m.plan(CASES), FRAMES)


def check(out):
    cases = []
    for line in (out / 'trace.txt').read_text(encoding='utf-8').splitlines():
        action, x, y = line.split()
        if action == 'reset': cases.append([])
        if cases: cases[-1].append((action, m.cell_of(x, y)))
    steps = m.plan(CASES)
    assert len(cases) == len(steps), [len(c) for c in cases]
    summary = {}
    for (name, s, _, live, final), records in zip(steps, cases):
        assert records[0] == ('reset', m.START) and records[-1][1] == m.GOAL, (name, records)
        route, cost, pivot, setting = [m.START], 0, None, s
        for action, p in records[1:]:
            if live and action == 'bridge':
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
            assert set(suffix) & m.FORD, (name, 'the laid bridge is taken', suffix)
        else:
            assert cost == m.minimum(m.START, s), (name, cost, m.minimum(m.START, s))
            assert bool(set(route) & m.FORD) == s[1], (name, route)
        if s[0] == 8 and not s[1]:   # with the bridge the straight south road is cheapest
            assert any(a[0] != b[0] and a[1] != b[1] for a, b in zip(route, route[1:])), (name, route)
        summary[name] = {'steps': len(route) - 1, 'cost': cost, 'cells': route}
        if pivot: summary[name]['bridgeLaidAt'] = pivot
    (out / 'result.json').write_bytes((json.dumps(summary, separators=(',', ':')) + '\n').encode())
    print({name: (r['steps'], r['cost']) for name, r in summary.items()}, flush=True)


def play(player, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Routes.inputreplay')
    env = dict(os.environ, MONOON_ROUTE_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(player.resolve()), '--replay-input', str(out / 'Routes.inputreplay'), str(out / 'slot')], env=env, check=True)
    check(out)


if __name__ == '__main__':
    {'prepare': prepare, 'play': play, 'check': check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
