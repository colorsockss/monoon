"""Roadmap 16.9: writes SeedLab's Meadow sheet, tileset and scene, and holds the independent blob/random oracle the checks use.

Run from the repository root: python3 engine/tests/SeedLab.Autotile/meadow.py samples/SeedLab
"""
import hashlib
import math
from pathlib import Path
import struct
import sys
import uuid
import zlib

CELL, COLUMNS, ROWS = 16, 8, 8
HEDGE, GRASS = 1, 51           # tile IDs: sheet cell index + 1
HEDGE_SEED, GRASS_SEED, RANDOM = 7, 2026, 4
WATER, TORCH = 57, 60          # animated tiles on the last row: (first tile, frames, fps)
ANIMATED = {WATER: (3, 4), TORCH: (4, 8)}
NAMESPACE = uuid.UUID('f308a024-f67d-5f58-a81c-f501e0b42828')  # SeedLab project id
IDS = {name: str(uuid.uuid5(NAMESPACE, name)) for name in
       ['Assets/Meadow.png', 'Assets/Meadow.tileset', 'Scenes/Meadow.scene', 'Camera', 'Grass', 'Hedge', 'Water', 'Canvas', 'Title', 'Hint']}
AROUND = [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)]  # N E S W NE SE SW NW


def reduce(m):
    return m & 15 | (m & 16 if m & 3 == 3 else 0) | (m & 32 if m & 6 == 6 else 0) | \
        (m & 64 if m & 12 == 12 else 0) | (m & 128 if m & 9 == 9 else 0)


BLOB = sorted({reduce(m) for m in range(256)})
assert len(BLOB) == 47 and BLOB[:16] == list(range(16))


def pick(x, y, seed, count):
    m = 0xFFFFFFFF
    h = ((x & m) * 0x8DA6B343 ^ (y & m) * 0xD8163841 ^ (seed & m) * 0xCB1AB31F) & m
    h ^= h >> 16; h = h * 0x7FEB352D & m; h ^= h >> 15; h = h * 0x846CA68B & m; h ^= h >> 16
    return h % count


def display(cells, x, y, brush):
    """The art the engine must draw for brush at (x, y), computed without the engine."""
    if brush in ANIMATED:
        return brush
    if brush == GRASS:
        return GRASS + pick(x, y, GRASS_SEED, RANDOM)
    mask = sum(1 << i for i, (dx, dy) in enumerate(AROUND) if cells.get((x + dx, y + dy)) == HEDGE)
    index = BLOB.index(reduce(mask))
    if index != 46:
        return HEDGE + index
    choice = pick(x, y, HEDGE_SEED, RANDOM)
    return HEDGE + 46 if choice == 0 else HEDGE + 46 + choice


def hedge_cells():
    """Shapes that cover all 47 masks and cross the chunk seams at x = -1|0 and y = -1|0."""
    cells = {(x, y) for x in range(-8, 8) for y in range(-6, 6)} - {(x, y) for x in range(-3, 3) for y in range(-2, 2)}
    cells -= {(5, -4), (-6, 3), (0, -6), (7, 2), (-8, -3)}          # pinholes and edge notches
    cells |= {(15, y) for y in range(-10, 11)} | {(-14, -6), (-13, -6), (-14, -5), (-15, -5)} | {(19 + i, -8 + i) for i in range(4)}
    cells |= {(x, y) for x in range(-22, -17) for y in range(-3, 3)} - {(-20, -2), (-20, 1), (-22, 0), (-18, -1)}
    cells |= {(9, 9), (20, 6), (21, 6), (21, 7)} | {(x, y) for x in range(18, 23) for y in range(9, 13)} - {(19, 10), (21, 11)}
    # One 3x3 specimen per blob mask the shapes above miss, centres 4 cells apart along the bottom rows.
    seen = {BLOB.index(reduce(sum(1 << i for i, (dx, dy) in enumerate(AROUND) if (x + dx, y + dy) in cells))) for x, y in cells}
    for k, index in enumerate(i for i in range(47) if i not in seen):
        cx, cy = -24 + 4 * (k % 8), 8 + 4 * (k // 8)
        cells |= {(cx, cy)} | {(cx + dx, cy + dy) for i, (dx, dy) in enumerate(AROUND) if BLOB[index] >> i & 1}
    return cells


GRASS_AREA = [(x, y) for y in range(-14, 14) for x in range(-25, 25)]
# The pond fills the hedge's inner gap across both chunk seams; two torches stand on the hedge.
WATER_CELLS = {**{(x, y): WATER for x in range(-3, 3) for y in range(-2, 2)}, (-4, -3): TORCH, (3, 2): TORCH}
STEP = struct.unpack('f', struct.pack('f', 1 / 60))[0]


def frame(tile, tick):
    """The animation frame drawn at a tick: game seconds are the float tick step summed in double, as GameTime does."""
    if tile not in ANIMATED:
        return tile
    count, fps = ANIMATED[tile]
    seconds = 0.0
    for _ in range(tick):
        seconds += STEP
    return tile + math.floor(seconds * fps) % count


def png(path, pixels, width, height):
    raw = b''.join(b'\0' + bytes(c for px in pixels[y * width:(y + 1) * width] for c in px) for y in range(height))
    def chunk(kind, data): return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
                     chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def sheet():
    w, h = COLUMNS * CELL, ROWS * CELL
    px = [(0, 0, 0, 0)] * (w * h)
    hedge, rim, berry = (47, 107, 58, 255), (24, 66, 34, 255), (200, 67, 75, 255)
    grass, tuft, flower, sun = (125, 186, 90, 255), (94, 153, 68, 255), (246, 244, 232, 255), (240, 200, 70, 255)
    def put(cell, x, y, c): px[(cell // COLUMNS * CELL + y) * w + cell % COLUMNS * CELL + x] = c
    for index, mask in enumerate(BLOB + [255, 255, 255]):
        for y in range(CELL):
            for x in range(CELL):
                edge = (y < 3 and not mask & 1) or (x > 12 and not mask & 2) or (y > 12 and not mask & 4) or (x < 3 and not mask & 8)
                notch = (x > 12 and y < 3 and not mask & 16) or (x > 12 and y > 12 and not mask & 32) or \
                        (x < 3 and y > 12 and not mask & 64) or (x < 3 and y < 3 and not mask & 128)
                if edge or notch: continue
                outline = (y == 3 and not mask & 1) or (x == 12 and not mask & 2) or (y == 12 and not mask & 4) or (x == 3 and not mask & 8)
                put(index, x, y, rim if outline else hedge)
        if index >= 47:
            for bx, by in [[(4, 5), (10, 9)], [(11, 3), (6, 12)], [(3, 10), (12, 7)]][index - 47]:
                for dx in (0, 1):
                    for dy in (0, 1): put(index, bx + dx, by + dy, berry)
    for i in range(RANDOM):
        cell = GRASS - 1 + i
        for y in range(CELL):
            for x in range(CELL): put(cell, x, y, grass)
        marks = [[], [(4, 5, flower), (5, 5, sun), (4, 6, flower)], [(10, 9, sun), (11, 9, sun), (10, 10, sun)],
                 [(3, 12, tuft), (4, 11, tuft), (11, 4, tuft), (12, 3, tuft)]][i]
        for x, y, c in marks: put(cell, x, y, c)
    deep, light, foam = (52, 112, 186, 255), (98, 160, 222, 255), (226, 240, 250, 255)
    for f in range(3):
        for y in range(CELL):
            for x in range(CELL):
                wave = (x + 2 * y + 5 * f) % 16
                put(WATER - 1 + f, x, y, foam if wave == 0 else light if wave < 4 else deep)
    post, flames = (82, 58, 40, 255), [(255, 214, 92, 255), (250, 150, 52, 255), (232, 88, 40, 255)]
    for f in range(4):
        cell = TORCH - 1 + f
        for y in range(9, 16):
            for x in range(7, 9): put(cell, x, y, post)
        height = [5, 7, 6, 4][f]
        for y in range(9 - height, 9):
            for x in range(6 - (y > 6), 10 + (y > 6)):
                put(cell, x, y, flames[(y + f) % 3])
    return px, w, h


def write(project):
    project = Path(project)
    px, w, h = sheet()
    png(project / 'Assets/Meadow.png', px, w, h)
    tiles = []
    for index in range(COLUMNS * ROWS):
        tid = index + 1
        tile = f'- id: {tid}\n  source: {{x: {index % COLUMNS * CELL}, y: {index // COLUMNS * CELL}, width: {CELL}, height: {CELL}}}\n'
        tile += '  solid: true\n' if tid == HEDGE else '  solid: false\n'
        if tid == HEDGE: tile += f'  autoEdges: 8\n  randomTiles: {RANDOM}\n  randomSeed: {HEDGE_SEED}\n'
        if tid == GRASS: tile += f'  randomTiles: {RANDOM}\n  randomSeed: {GRASS_SEED}\n'
        if tid in ANIMATED: tile += f'  frames: {ANIMATED[tid][0]}\n  fps: {ANIMATED[tid][1]}\n'
        tiles.append(tile)
    (project / 'Assets/Meadow.tileset').write_text(
        f'image: {IDS["Assets/Meadow.png"]}\ntileSize: {{width: {CELL}, height: {CELL}}}\nmargin: 0\nspacing: 0\n'
        f'nextId: {COLUMNS * ROWS + 1}\ntiles:\n' + ''.join(tiles), encoding='utf-8')

    def chunks(cells):
        pages = {}
        for (x, y), tile in cells.items():
            pages.setdefault((x >> 5, y >> 5), []).append(((y & 31) * 32 + (x & 31), tile))
        return ''.join(f'    - x: {cx}\n      y: {cy}\n      cells: [' + ', '.join(f'{{index: {i}, tile: {t}}}' for i, t in sorted(v)) + ']\n'
                       for (cx, cy), v in sorted(pages.items(), key=lambda p: (p[0][1], p[0][0])))

    def layer(name, cells, order):
        return (f'- id: {IDS[name]}\n  name: {name}\n  x: 400\n  y: 225\n  width: 1\n  height: 1\n  components:\n  - type: tilemap2D\n'
                f'    tileset: {IDS["Assets/Meadow.tileset"]}\n    cellSize: {{width: 16, height: 16}}\n    order: {order}\n    chunks:\n'
                + chunks(cells))

    def text(name, y, size, value):
        return (f'- id: {IDS[name]}\n  name: {name}\n  parentId: {IDS["Canvas"]}\n  x: 24\n  y: {y}\n  width: 640\n  height: {size + 12}\n'
                f'  components:\n  - {{type: uiRect}}\n  - {{type: uiText, text: "{value}", size: {size}, color: "#F4F1E4", insetX: 0, insetY: 0}}\n')
    scene = ('schemaVersion: 14\nobjects:\n'
             f'- id: {IDS["Camera"]}\n  name: Camera\n  x: 400\n  y: 225\n  width: 1\n  height: 1\n  components:\n  - type: camera2D\n'
             f'  - type: script\n    script: RouteSceneSwitch\n    values: {{Destination: {{Id: "0cfbf6ac-2359-53aa-abc6-f603bdf4dbee"}}}}\n'
             + layer('Grass', {c: GRASS for c in GRASS_AREA}, 0) + layer('Hedge', {c: HEDGE for c in hedge_cells()}, 1) + layer('Water', WATER_CELLS, 2) +
             f'- id: {IDS["Canvas"]}\n  name: Canvas\n  x: 0\n  y: 0\n  width: 800\n  height: 450\n  components:\n  - type: uiCanvas\n'
             + text('Title', 12, 26, 'Hedge garden') +
             text('Hint', 46, 14, 'Edit Tiles on Hedge to paint hedges. Water and torches animate. G: Seed Lab'))
    (project / 'Scenes/Meadow.scene').write_text(scene, encoding='utf-8')
    for name in ['Assets/Meadow.png', 'Assets/Meadow.tileset', 'Scenes/Meadow.scene']:
        (project / (name + '.meta')).write_text(f'id: {IDS[name]}\n', encoding='utf-8')
    manifest = project / '.monoon-assets'
    rows = {}
    for entry in manifest.read_text(encoding='utf-8').split('- id: ')[1:]:
        lines = entry.splitlines()
        rows[lines[1].split('path: ')[1]] = lines[0]
    for name in ['Assets/Meadow.png', 'Assets/Meadow.tileset', 'Scenes/Meadow.scene']: rows[name] = IDS[name]
    manifest.write_text(''.join(f'- id: {rows[p]}\n  path: {p}\n  hash: {hashlib.sha256((project / p).read_bytes()).hexdigest().upper()}\n'
                                for p in sorted(rows)), encoding='utf-8')


if __name__ == '__main__':
    write(sys.argv[1])
