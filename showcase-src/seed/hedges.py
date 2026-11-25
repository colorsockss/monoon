# Writes SeedLab's hedge garden tiles for the route showcase: Assets/Showcase/Hedges.png + Hedges.tileset.
#   python3 showcase-src/seed/hedges.py            (route.py lays them out; run this first)
#
# One 8 x 8 sheet of 32-unit tiles in Bush.png's palette:
#   ids 1-50   the hedge brush: 47 blob shapes (8 neighbours) and three berry variants of the full shape (random 4, seed 7)
#   ids 51-54  flower tufts (random 4, seed 2026)
#   ids 57-60  a torch whose flame flickers (4 frames at 8 fps)
#   ids 61-63  ripples drifting over the river (3 frames at 4 fps)
# Each blob tile is built from four 16 x 16 quarters; a quarter looks at its side, top-or-bottom and diagonal neighbours and is
# an outer corner, a top/bottom edge, a left/right edge, an inner corner or solid hedge. The leaf texture repeats every tile, so
# neighbouring tiles join without seams.
import pathlib, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import save

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/SeedLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("0cfbf6ac-2359-53aa-abc6-f603bdf4dbee")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
T, COLUMNS, ROWS = 32, 8, 8
HEDGE, FLOWERS, TORCH, RIPPLE = 1, 51, 57, 61
HEDGE_SEED, FLOWER_SEED, RANDOM = 7, 2026, 4
TORCH_FRAMES, TORCH_FPS, RIPPLE_FRAMES, RIPPLE_FPS = 4, 8, 3, 4
AROUND = [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)]  # N E S W NE SE SW NW, bit i


def reduce(m):
    """A diagonal only counts when both sides next to it are hedge (the engine's 47-shape rule)."""
    return m & 15 | (m & 16 if m & 3 == 3 else 0) | (m & 32 if m & 6 == 6 else 0) | \
        (m & 64 if m & 12 == 12 else 0) | (m & 128 if m & 9 == 9 else 0)


BLOB = sorted({reduce(m) for m in range(256)})
assert len(BLOB) == 47

hexc = lambda h: (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), 255)
OUTLINE, DARK, MID, LEAF, LIGHT, SHINE = map(hexc, ["#261F2A", "#485D2F", "#5A6B2F", "#7E9436", "#B6B430", "#C3CD27"])
BERRY, BLOSSOM, PETAL_Y, PETAL_P, PETAL_C, STEM = map(hexc, ["#E78B6C", "#EFD262", "#EFD262", "#8D7AB5", "#E78B6C", "#7E9436"])
SHADOW = (38, 31, 42, 110)
CLEAR = (0, 0, 0, 0)
M, R = 3, 9   # edge inset and outer corner radius


def noise(x, y, seed=0):
    h = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = (h ^ h >> 13) * 1274126177 & 0xFFFFFFFF
    return (h ^ h >> 16) & 0xFFFF


def inside(mask, x, y):
    """Is pixel (x, y) of a 32 x 32 tile hedge, for neighbour bits mask? Outside the tile, ask the neighbouring cell."""
    if not (0 <= x < T and 0 <= y < T):
        dx, dy = (x >= T) - (x < 0), (y >= T) - (y < 0)
        return bool(mask >> AROUND.index((dx, dy)) & 1)
    left, top = x < T // 2, y < T // 2
    lx, ly = (x if left else T - 1 - x), (y if top else T - 1 - y)
    side = mask >> (3 if left else 1) & 1
    edge = mask >> (0 if top else 2) & 1
    corner = mask >> {(True, True): 7, (False, True): 4, (True, False): 6, (False, False): 5}[(left, top)] & 1
    if side and edge:
        return bool(corner) or not (lx < M and ly < M and (lx - M) ** 2 + (ly - M) ** 2 >= M * M)
    if side: return ly >= M
    if edge: return lx >= M
    cx, cy = max(0, M + R - lx), max(0, M + R - ly)
    return lx >= M and ly >= M and cx * cx + cy * cy <= R * R


def hedge_tile(mask, variant=0):
    tile = [[CLEAR] * T for _ in range(T)]
    for y in range(T):
        for x in range(T):
            if not inside(mask, x, y):
                # A soft shadow in the gap just below the hedge.
                if inside(mask, x, y - 1) or inside(mask, x, y - 2): tile[y][x] = SHADOW
                continue
            if not all(inside(mask, x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                tile[y][x] = OUTLINE
                continue
            # Leaf clusters on a staggered 8-unit grid (periodic in the tile, so neighbours join): each cluster is lit on its
            # upper left and shaded on its lower right over a dark gap; the hedge's top edge catches the light.
            tone = DARK if noise(x, y, 5) % 5 else MID
            for gx in range(-8, T + 8, 8):
                for gy in range(-8, T + 8, 8):
                    cx = gx + (4 if (gy // 8) % 2 else 0) + noise(gx % T, gy % T, 7) % 3 - 1
                    cy = gy + noise(gx % T, gy % T, 8) % 3 - 1
                    dx, dy = x - cx, y - cy
                    if dx * dx + dy * dy <= 12:
                        tone = LIGHT if dx + dy <= -3 else LEAF if dx + dy <= 2 else MID
            if not inside(mask, x, y - 2): tone = SHINE if noise(x, y, 9) % 3 else LIGHT
            elif not inside(mask, x, y + 2): tone = DARK
            tile[y][x] = tone
    if variant:   # berries or blossoms on the full shape
        for k in range(5):
            bx, by = 5 + noise(k, variant, 11) % 21, 5 + noise(k, variant, 13) % 21
            colour = [BERRY, BLOSSOM, PETAL_P][variant - 1]
            for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)): tile[by + dy][bx + dx] = colour
            tile[by][bx] = SHINE
    return tile


def flower_tile(variant):
    """A 32 x 32 window of the showcase's own Flowers.png (54 x 32), at four offsets and mirrors."""
    from pngio import load
    w, h, rows = load(ART / "Flowers.png")
    ox, flip = [(0, False), (22, False), (0, True), (11, True)][variant]
    tile = [[CLEAR] * T for _ in range(T)]
    for y in range(min(T, h)):
        for x in range(T):
            sx = ox + (T - 1 - x if flip else x)
            if 0 <= sx < w: tile[y][x] = tuple(rows[y][sx * 4:sx * 4 + 4])
    return tile


def torch_tile(frame):
    tile = [[CLEAR] * T for _ in range(T)]
    wood, wood_dark = hexc("#8A5A3A"), hexc("#5C3A21")
    for y in range(14, 31):
        for x in range(14, 18): tile[y][x] = wood if x < 17 else wood_dark
    for x in range(12, 20): tile[14][x] = wood_dark; tile[15][x] = wood_dark
    for y in range(14, 31): tile[y][13] = OUTLINE; tile[y][18] = OUTLINE
    tile[31][13:19] = [OUTLINE] * 6
    # Flame: a teardrop whose tip sways and height breathes over four frames.
    sway, height = [0, 1, 0, -1][frame], [9, 11, 10, 8][frame]
    for y in range(13 - height, 14):
        t = (13 - y) / height
        half = max(0.0, 3.2 * (1 - t) ** 0.6 - 0.2)
        cx = 16 + sway * t
        for x in range(10, 23):
            d = abs(x + .5 - cx)
            if d <= half: tile[y][x] = PETAL_Y if d < half * .45 and t < .7 else hexc("#E9B045") if d < half * .8 else BERRY
    return tile


def ripple_tile(frame):
    """Pale glints over the river picture, drifting a third of the tile each frame (the loop is seamless)."""
    tile = [[CLEAR] * T for _ in range(T)]
    glint, faint = (226, 238, 255, 200), (159, 196, 255, 140)
    for k, (gx, gy, length) in enumerate(((3, 6, 6), (17, 12, 8), (8, 21, 5), (22, 27, 6), (26, 3, 4))):
        x0 = (gx + frame * 11) % T
        for i in range(length):
            tile[gy][(x0 + i) % T] = glint if 0 < i < length - 1 else faint
    return tile


def sheet():
    cells = {}
    for i, m in enumerate(BLOB): cells[HEDGE + i] = hedge_tile(m)
    for v in range(1, RANDOM): cells[HEDGE + 46 + v] = hedge_tile(BLOB[46], v)
    for v in range(RANDOM): cells[FLOWERS + v] = flower_tile(v)
    for f in range(TORCH_FRAMES): cells[TORCH + f] = torch_tile(f)
    for f in range(RIPPLE_FRAMES): cells[RIPPLE + f] = ripple_tile(f)
    rows = []
    for y in range(ROWS * T):
        line = bytearray()
        for c in range(COLUMNS):
            tile = cells.get(y // T * COLUMNS + c + 1)
            for x in range(T): line += bytes(tile[y % T][x] if tile else CLEAR)
        rows.append(line)
    return rows


def write():
    save(ART / "Hedges.png", COLUMNS * T, ROWS * T, sheet())
    meta = lambda n: (ART / (n + ".meta")).exists() or (ART / (n + ".meta")).write_text(f"id: {uid(n)}\n")
    meta("Hedges.png")
    image = (ART / "Hedges.png.meta").read_text().split()[1]
    tiles = []
    for index in range(COLUMNS * ROWS):
        tid = index + 1
        if tid >= RIPPLE + RIPPLE_FRAMES or FLOWERS + RANDOM <= tid < TORCH: continue
        tile = f"- id: {tid}\n  source: {{x: {index % COLUMNS * T}, y: {index // COLUMNS * T}, width: {T}, height: {T}}}\n  solid: false\n"
        if tid == HEDGE: tile += f"  terrain: hedge\n  autoEdges: 8\n  randomTiles: {RANDOM}\n  randomSeed: {HEDGE_SEED}\n"
        if tid == FLOWERS: tile += f"  terrain: flowers\n  randomTiles: {RANDOM}\n  randomSeed: {FLOWER_SEED}\n"
        if tid == TORCH: tile += f"  terrain: torch\n  frames: {TORCH_FRAMES}\n  fps: {TORCH_FPS}\n"
        if tid == RIPPLE: tile += f"  terrain: ripple\n  frames: {RIPPLE_FRAMES}\n  fps: {RIPPLE_FPS}\n"
        tiles.append(tile)
    (ART / "Hedges.tileset").write_text(f"image: {image}\ntileSize: {{width: {T}, height: {T}}}\nmargin: 0\nspacing: 0\n"
                                         f"nextId: {COLUMNS * ROWS + 1}\ntiles:\n" + "".join(tiles))
    meta("Hedges.tileset")


if __name__ == "__main__":
    write()
    print("wrote", ART / "Hedges.png", ART / "Hedges.tileset")
