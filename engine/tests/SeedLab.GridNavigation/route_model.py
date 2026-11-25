"""Independent model of the SeedLab showcase route map (showcase-src/seed/route.py) for SeedLab.GridNavigation and
SeedLab.TileTerrain: which cells exist, what entering them costs, which tile each one shows, and the cheapest route.
A setting is (directions, south bridge laid, rain on the north road)."""
import heapq
import itertools
import re
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = 'f308a024-f67d-5f58-a81c-f501e0b42828'
SCENE = '4b08196c-cd93-50b1-9a6a-ef1a4ecefbe4'  # Scenes/ShowcaseRoute.scene
START_SCENE = 'startScene: cdfa4adc-9fd0-58dc-bdff-271497eca1f0'  # the sample starts in ShowcaseMaze
MAP = ["HHHHHHHH~~HHHHHHHH",
       "H.======~~======.H",
       "H.=.....~~.....=.H",
       "H.=..H..~~..H..=.H",
       "H.=.....~~.....=.H",
       "H=======~~=======H",
       "H.......~~.......H",
       "H..H....~~....H..H",
       "HHHHHHHH~~HHHHHHHH"]
W, H = len(MAP[0]), len(MAP)
CELL = 32
START, GOAL = (1, 5), (15, 5)
NORTH_BRIDGE = {(8, 1), (9, 1)}
FORD = {(8, 5), (9, 5)}
NORTH = {(x, 1) for x in range(3, 8)}
# Reference costs, written independently of Field.tileset (TileTerrain also compares them with the asset).
COST = {'grass': 3, 'road': 1, 'mud': 8, 'bridge': 1}
KEYS = {'V': 0, 'O': 1, 'B': 2}  # index into the setting


def terrain(p, s):
    x, y = p
    c = MAP[y][x]
    if c == 'H': return 'hedge'
    if c == '~': return 'bridge' if p in NORTH_BRIDGE or p in FORD and s[1] else 'water'
    if c == '=': return 'mud' if p in NORTH and s[2] else 'road'
    return 'grass'


def tile(p, s):
    """The Field.tileset id route.py paints (grass and road have look-alike variants), after the driver's edits."""
    x, y = p
    t = terrain(p, s)
    h = (x * 7 + y * 13) % 4
    return {'hedge': 6, 'water': 4, 'bridge': 5, 'mud': 3, 'road': 2 if h < 2 else 10, 'grass': [1, 7, 8, 9][h]}[t]


def weight(p, s):
    if not (0 <= p[0] < W and 0 <= p[1] < H): return 0
    return COST.get(terrain(p, s), 0)


def neighbors(cell, s):
    x, y = cell
    for dx, dy in itertools.product((-1, 0, 1), repeat=2):
        p = x + dx, y + dy
        if p == cell or not weight(p, s): continue
        diagonal = dx != 0 and dy != 0
        if diagonal and (s[0] == 4 or not (weight((p[0], y), s) and weight((x, p[1]), s))): continue
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


def after(s, key):
    s = list(s)
    s[KEYS[key]] = (8 if s[0] == 4 else 4) if key == 'V' else not s[KEYS[key]]
    return tuple(s)


def plan(cases):
    """cases: (name, directions, bridge, rain, live key or None). Per case: the setting before walking, the toggles that
    reach it from the previous case's end, the live key and the setting after it."""
    current, steps = (4, False, False), []
    for name, *setting, live in cases:
        target = tuple(setting)
        toggles = [k for k, i in KEYS.items() if current[i] != target[i]]
        final = after(target, live) if live else target
        steps.append((name, target, toggles, live, final))
        current = final
    return steps


def cell_of(x, y):
    """The courier's local position (feet low in the cell, as RouteCourier places him) back to its cell."""
    px, py = float(x) + 21 - CELL / 2, float(y) + 48 - CELL / 2
    assert px % CELL == py % CELL == 0, (x, y)
    return int(px // CELL), int(py // CELL)


def prepare(sample, destination, trace):
    """Copy SeedLab, start it in the showcase route, and swap RouteCourier for DriveRoute plus the given trace script."""
    shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'DriveRoute.source', destination / 'Scripts/DriveRoute.cs')
    shutil.copyfile(Path(trace), destination / f'Scripts/{Path(trace).stem}.cs')
    settings = destination / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert text.count(START_SCENE) == 1
    settings.write_text(text.replace(START_SCENE, f'startScene: {SCENE}'), encoding='utf-8')
    scene = destination / 'Scenes/ShowcaseRoute.scene'
    text = scene.read_text(encoding='utf-8')
    courier = re.search(r'- id: (\S+)\n  name: Courier\n', text).group(1)
    tour = re.findall(r'  - type: script\n    script: Showcase/RouteCourier\n    values: .*\n', text)
    assert len(tour) == 1
    scripts = (f'  - type: script\n    script: DriveRoute\n    values: {{"Courier": "{courier}"}}\n'
               f'  - type: script\n    script: {Path(trace).stem}\n    values: {{"Courier": "{courier}"}}\n')
    scene.write_text(text.replace(tour[0], scripts), encoding='utf-8')


def recording(path, steps, frames):
    events = []
    def press(frame, key):
        for offset, down in [(0, 'true'), (1, 'false')]:
            tick = frame + offset
            events.append(f'- frame: {tick}\n  timeTicks: {tick * 166667}\n  input: {{kind: key, control: "{key}", down: {down}}}\n')
    for index, (_, _, toggles, live, _) in enumerate(steps):
        base = 4 + index * frames
        press(base, 'R')
        for offset, key in enumerate(toggles): press(base + 3 + 3 * offset, key)
        press(base + 15, 'Space')
        if live: press(base + 43, live)
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' +
                    ', '.join(['166667'] * (4 + len(steps) * frames)) + ']\nevents:\n' + ''.join(events), encoding='utf-8')
