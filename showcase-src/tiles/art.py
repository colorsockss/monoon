# Rebuilds samples/KeyRoomTiles/Assets/Showcase: one tileset per grid layout (square, isometric, hex pointy, hex flat), all drawn
# here (nothing generated). Each sheet, row major with 8 columns:
#   cells 0-2   grass, picked per cell (tile 1, randomTiles 3)
#   cells 3-5   water, three frames (tile 4, frames 3)
#   cells 6-    dirt road terrain brush (tile 7): 16 cells (4 edges: square, isometric) or 64 cells (6 axial edges: hex)
# The road in each cell runs from the centre to the middle of every connected edge, using that layout's neighbour directions
# (docs/manual/tilemaps.md, "오토타일 브러시"), so the pieces join whatever the grid. Rectangular cells are clipped to the
# layout's outline when drawn. One art pixel = 2 world units.  Run:  python3 showcase-src/tiles/art.py
import math, pathlib, random, shutil, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = SAMPLES / "KeyRoomTiles/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")
shutil.copy(SAMPLES / "SeedLab/Assets/Showcase/Villager.png", OUT / "Villager.png")
COLUMNS = 8

# Cell size in world units and the centre-to-centre neighbour offsets, as fractions of the cell size, in brush mask order.
LAYOUTS = {
    "Square": (32, 32, [(0, -1), (1, 0), (0, 1), (-1, 0)]),
    "Iso": (48, 24, [(.5, -.5), (.5, .5), (-.5, .5), (-.5, -.5)]),
    "HexPointy": (36, 42, [(1, 0), (.5, .75), (-.5, .75), (-1, 0), (-.5, -.75), (.5, -.75)]),
    "HexFlat": (42, 36, [(.75, .5), (0, 1), (-.75, .5), (-.75, -.5), (0, -1), (.75, -.5)]),
}
GRASS = [(104, 156, 66), (92, 142, 58), (128, 176, 82)]
WATER, WAVE, DEEP = (56, 112, 172), (128, 182, 228), (44, 92, 150)
ROAD, RIM = (206, 170, 112), (150, 112, 70)


def grass(w, h, seed):
    rng = random.Random(seed)
    art = [[GRASS[0]] * w for _ in range(h)]
    for _ in range(w * h // 7):
        x, y = rng.randrange(w), rng.randrange(h)
        art[y][x] = GRASS[1 + rng.randrange(2)]
    return art


def water(w, h, frame):
    art = [[WATER if (x + 2 * y) % 9 else DEEP for x in range(w)] for y in range(h)]
    for y in range(1, h, 4):                    # short wave dashes that drift one pixel per frame
        for x in range(w):
            if (x + frame * 2 + y * 3) % 8 < 3: art[y][x] = WAVE
    return art


def road(w, h, deltas, mask, iso):
    """A grass cell with a dirt road from the centre to the middle of each connected edge. Works in world units (2 per art
    pixel). Isometric distances count y twice, so the road keeps its width on the squashed ground."""
    art = grass(w, h, 1000 + mask)
    Wu, Hu, k = w * 2, h * 2, 2 if iso else 1
    cx, cy = Wu / 2, Hu / 2
    ends = [(cx + dx * Wu / 2, cy + dy * Hu / 2) for i, (dx, dy) in enumerate(deltas) if mask >> i & 1]
    r = min(Wu, Hu * k) * .2
    for py in range(h):
        for px in range(w):
            x, y = (px + .5) * 2, (py + .5) * 2
            d = math.hypot(x - cx, (y - cy) * k)                        # the isolated stub is a round patch
            for bx, by in ends:
                vx, vy = bx - cx, (by - cy) * k
                t = max(0, min(1, ((x - cx) * vx + (y - cy) * k * vy) / (vx * vx + vy * vy)))
                d = min(d, math.hypot(x - cx - vx * t, (y - cy) * k - vy * t))
            if d < r: art[py][px] = ROAD
            elif d < r + 2.2: art[py][px] = RIM
    return art


tilesets = {}
for name, (W, H, deltas) in LAYOUTS.items():
    w, h = W // 2, H // 2                        # art pixels per cell
    edges = len(deltas)
    cells = [grass(w, h, s) for s in (1, 2, 3)] + [water(w, h, f) for f in range(3)]
    cells += [road(w, h, deltas, m, name == "Iso") for m in range(1 << edges)]
    rows = (len(cells) + COLUMNS - 1) // COLUMNS
    sheet = [[(0, 0, 0, 0)] * (COLUMNS * w) for _ in range(rows * h)]
    for i, art in enumerate(cells):
        ox, oy = i % COLUMNS * w, i // COLUMNS * h
        for y in range(h):
            for x in range(w):
                c = art[y][x]
                sheet[oy + y][ox + x] = (*c, 255) if len(c) == 3 else c
    rows_px = [bytearray(b"".join(bytes(c) * 2 for c in line)) for line in sheet for _ in (0, 1)]
    save(OUT / f"{name}.png", COLUMNS * w * 2, rows * h * 2, rows_px)
    tilesets[name] = (W, H, len(cells), edges)

# Cell outlines for the cursor: a bright 1-pixel border of each layout's shape, 2 units per pixel.
def outline(name, W, H):
    w, h = W // 2, H // 2
    poly = {"Square": [(0, 0), (1, 0), (1, 1), (0, 1)], "Iso": [(.5, 0), (1, .5), (.5, 1), (0, .5)],
            "HexPointy": [(.5, 0), (1, .25), (1, .75), (.5, 1), (0, .75), (0, .25)],
            "HexFlat": [(.25, 0), (.75, 0), (1, .5), (.75, 1), (.25, 1), (0, .5)]}[name]
    def inside(x, y):
        c = False
        for i in range(len(poly)):
            (ax, ay), (bx, by) = poly[i], poly[i - 1]
            if (ay > y) != (by > y) and x < ax + (y - ay) * (bx - ax) / (by - ay): c = not c
        return c
    art = []
    for py in range(h):
        line = []
        for px in range(w):
            u, v = (px + .5) / w, (py + .5) / h
            here = inside(u, v)
            edge = here and any(not inside(u + dx / w, v + dy / h) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            line.append((255, 236, 140, 255) if edge else (255, 236, 140, 40) if here else (0, 0, 0, 0))
        art.append(line)
    save(OUT / f"Cursor{name}.png", w * 2, h * 2, [bytearray(b"".join(bytes(c) * 2 for c in line)) for line in art for _ in (0, 1)])


for name, (W, H, _) in LAYOUTS.items(): outline(name, W, H)
print({k: v[2] for k, v in tilesets.items()})
