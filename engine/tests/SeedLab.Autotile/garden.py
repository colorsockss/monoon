"""Independent model for SeedLab.Autotile: the route showcase's Hedges and Decor layers (showcase-src/seed/route.py and
hedges.py) and the art the engine must draw for them, computed without the engine: 47-shape blob hedges from 8 neighbours with
random full cells, random flower tufts, and frame-animated torches and river ripples."""
import math
import struct

PROJECT = 'f308a024-f67d-5f58-a81c-f501e0b42828'
SCENE = '4b08196c-cd93-50b1-9a6a-ef1a4ecefbe4'  # Scenes/ShowcaseRoute.scene
CANVAS = 'd9912a76-0b44-5a47-bfe1-cfb7f1bc239e'
HEDGE, FLOWERS, TORCH, RIPPLE = 1, 51, 57, 61
HEDGE_SEED, FLOWER_SEED, RANDOM = 7, 2026, 4
ANIMATED = {TORCH: (4, 8), RIPPLE: (3, 4)}  # first tile: (frames, fps)
AROUND = [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)]  # N E S W NE SE SW NW
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
HEDGES = {(x, y): HEDGE for y, line in enumerate(MAP) for x, c in enumerate(line) if c == 'H'}
DECOR = {**{p: FLOWERS for p in ((4, 2), (12, 6), (5, 7), (13, 2))}, (16, 4): TORCH, (1, 6): TORCH,
         **{(x, y): RIPPLE for y in (0, 2, 3, 4, 6, 7, 8) for x in (8, 9)}}
STEP = struct.unpack('f', struct.pack('f', 1 / 60))[0]


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
    """The art the engine must draw for brush at (x, y)."""
    if brush in ANIMATED: return brush
    if brush == FLOWERS: return FLOWERS + pick(x, y, FLOWER_SEED, RANDOM)
    mask = sum(1 << i for i, (dx, dy) in enumerate(AROUND) if cells.get((x + dx, y + dy)) == HEDGE)
    index = BLOB.index(reduce(mask))
    if index != 46: return HEDGE + index
    choice = pick(x, y, HEDGE_SEED, RANDOM)
    return HEDGE + 46 if choice == 0 else HEDGE + 46 + choice


def frame(tile, tick):
    """An animated tile shows floor(game seconds x FPS) mod frames; game seconds sum the float tick step."""
    if tile not in ANIMATED: return tile
    seconds = 0.0
    for _ in range(tick): seconds += STEP
    count, fps = ANIMATED[tile]
    return tile + math.floor(seconds * fps) % count
