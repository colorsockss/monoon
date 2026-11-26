# Rebuilds samples/CollisionLab/Assets/Showcase (nothing generated; everything is drawn here or reused from other showcases).
#   python3 showcase-src/collision/art.py      then scene.py and casts.py
#
# Lantern field: Field.png, the night field's ground (grass with dark patches and a winding dirt path), one 400 × 225 picture
# drawn at 800 × 450 so each art pixel is 2 units like the other showcases' tiles (stored at 1 px per art pixel: a quarter of
# the bytes, identical on screen with integer scaling). Nine 5 × 5 stone kinds (pebbles = circle collider, blocks = box,
# shards = triangle polygon; three looks each), each dim and lantern-lit, are drawn one art pixel per world unit so 10,000
# fit one 800 × 450 view, and packed into Stones.atlas (one 42 × 21 sheet), so every stone draws from one texture in one
# batch, dim or lit. LanternRing.png marks the query circle; Lantern.png (SpriteLab's) hangs in it.
# Probe course: Probe.png (a steel sphere with an amber lens), SensorIdle / SensorActive (a rune plate), Gate.png (a white
# force field the scene tints per gate), GatePost.png and RampWall.png (KeyRoom's wall bricks cut to the polygon collider).
# Reused as they are: KeyRoom's floor, wall, cap and torch (with its flame clip), InventoryLab's fence, bush, flowers, well.
import hashlib, math, pathlib, shutil, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import load, save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
ROOT = SAMPLES / "CollisionLab"
OUT = ROOT / "Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
NS = uuid.UUID("5750e5bb-1700-5a5b-94c2-b6bd02a0356c")


def meta(name):   # same ids as scene.py's asset()
    m = OUT / (name + ".meta")
    if not m.exists(): m.write_text(f"id: {uuid.uuid5(NS, 'showcase/' + name)}\n")
    return m.read_text().split()[1]


def copy(sample, name, as_name=None):   # with the source's meta, so clips that name its frames keep working
    as_name = as_name or name
    shutil.copy(SAMPLES / sample / "Assets/Showcase" / name, OUT / as_name)
    if not (OUT / (as_name + ".meta")).exists(): shutil.copy(SAMPLES / sample / "Assets/Showcase" / (name + ".meta"), OUT / (as_name + ".meta"))


shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", OUT / "Panel.png")
for name in ("Floor.png", "WallFace.png", "WallCap.png", "TorchSconce.png", "TorchFlame1.png", "TorchFlame2.png", "TorchFlame3.png", "Flame.clip"):
    copy("KeyRoom", name)
for name in ("Fence.png", "Bush.png", "Flowers.png", "Well.png", "ShadowM.png"):
    copy("InventoryLab", name)
copy("SpriteLab", "Lantern.png")

hexc = lambda s: tuple(int(s[i:i + 2], 16) for i in (1, 3, 5))


def write(name, art, scale=1):   # art: rows of (r, g, b, a) or None
    rows = []
    for line in art:
        row = bytearray(b"".join(bytes(c if c and len(c) == 4 else (*c, 255) if c else (0, 0, 0, 0)) * scale for c in line))
        rows += [row] + [bytearray(row) for _ in range(scale - 1)]
    save(OUT / name, len(art[0]) * scale, len(art) * scale, rows)
    if name.endswith(".png"): meta(name)


def noise(x, y, cell, seed):   # smooth value noise in 0..1
    gx, gy = x / cell, y / cell
    x0, y0 = math.floor(gx), math.floor(gy)
    fx, fy = gx - x0, gy - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def h(i, j):
        n = (i * 73856093 ^ j * 19349663 ^ seed * 83492791) & 0xFFFFFFFF
        n = (n ^ n >> 13) * 1274126177 & 0xFFFFFFFF
        return ((n ^ n >> 16) & 0xFFFFFF) / 0xFFFFFF
    a, b, c, d = h(x0, y0), h(x0 + 1, y0), h(x0, y0 + 1), h(x0 + 1, y0 + 1)
    return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy


# ---- night field ground --------------------------------------------------------------------------------------------------
gw, gh, grass = load(SAMPLES / "InventoryLab/Assets/Showcase/Grass.png")   # 2 px per art pixel: read every other pixel
tile = lambda x, y: tuple(grass[(y % (gh // 2)) * 2][(x % (gw // 2)) * 8:(x % (gw // 2)) * 8 + 3])
DARK, DARKER = hexc("#5A6B2F"), hexc("#485D2F")
DIRT, DIRT_D, DIRT_L, PEB = hexc("#927058"), hexc("#835526"), hexc("#B98262"), hexc("#C5B4A4")
W, H = 400, 225
field = []
for y in range(H):
    line = []
    for x in range(W):
        n = .6 * noise(x, y, 26, 1) + .4 * noise(x, y, 9, 2)
        d = n + (.03 if (x + y) & 1 else -.03)   # checker dither where two shades meet
        c = DARKER if d < .30 else DARK if d < .40 else tile(x, y)
        path = 113 + 17 * math.sin(x / 50 + .5) + 6 * math.sin(x / 17 + 1.3)
        half = 7 + 3 * noise(x, 0, 23, 3) + (noise(x, y, 3, 4) - .5) * 2.6
        dist = abs(y - path)
        if dist < half:
            m = noise(x, y, 4, 5)
            c = DIRT_D if m < .34 else DIRT_L if m > .78 else DIRT
            if noise(x, y, 1.3, 6) > .93: c = PEB
        elif dist < half + 1.2: c = DARKER
        line.append(c)
    field.append(line)
write("Field.png", field)

# ---- stones: 5 × 5, the bottom row is a soft shadow ----------------------------------------------------------------------
SHAPES = {
    "Pebble1": [".HHM.", "HMMMD", "MMMDO", ".DDO.", ".sss."],
    "Pebble2": [".....", ".HHM.", "HMMMD", "MDDDO", ".sss."],
    "Pebble3": ["..HM.", ".HMMD", "HMMDO", ".MDO.", ".ss.."],
    "Block1": ["HHHHM", "HMMMD", "HMMMD", "MDDDO", "sssss"],
    "Block2": ["HHHM.", "HMDMD", "HMMDD", "MDDDO", ".ssss"],
    "Block3": ["HHM..", "HMDHM", "MDOMD", ".sMDO", "..ss."],
    "Shard1": ["H....", "HMM..", "HMMMD", "MDD..", "D...."],
    "Shard2": ["M....", "HHD..", "HMMDO", "MDO..", "O...."],
    "Shard3": ["H....", "MHM..", "MMHDD", "DMD..", "Ds..."],
}
ramp = lambda *c: tuple(map(hexc, c))
SLATE, GRANITE, MOSS = ramp("#B8B2CA", "#988CAA", "#726E8D", "#4C4F61"), ramp("#C5B4A4", "#B68975", "#927058", "#79485E"), ramp("#988CAA", "#726E8D", "#627155", "#3B3C2A")
FLINT, SAND = ramp("#B493A6", "#79485E", "#4C2F52", "#261C4B"), ramp("#CEAD88", "#C28F4C", "#927058", "#835526")
DIM = {"Pebble1": SLATE, "Pebble2": GRANITE, "Pebble3": MOSS, "Block1": SAND, "Block2": SLATE, "Block3": GRANITE,
       "Shard1": FLINT, "Shard2": SLATE, "Shard3": MOSS}
GOLD, AMBER, PALE = ramp("#F5E6A5", "#F1D45C", "#E1A320", "#835526"), ramp("#F1D586", "#E9881F", "#BB6422", "#7D2F29"), ramp("#F0EBCB", "#F1D586", "#E3AE5F", "#B26C4E")
LIT = {"Pebble": GOLD, "Block": AMBER, "Shard": PALE}
SHADOW = (23, 20, 41, 120)
cells = []
for name, art in SHAPES.items():
    for suffix, pal in (("", DIM[name]), ("Lit", LIT[name[:-1]])):
        px = dict(zip("HMDO", pal)) | {"s": SHADOW}
        pic = [[px.get(ch) for ch in line] for line in art]
        write(f"{name}{suffix}.png", pic)
        cells.append((f"{name}{suffix}.png", pic))
# Atlas: 6 × 3 cells, each with a one-pixel border copied from its edge (as the editor's Pack does).
COLS, STRIDE = 6, 7
sheet = [[None] * (COLS * STRIDE) for _ in range(STRIDE * -(-len(cells) // COLS))]
entries = []
for i, (name, pic) in enumerate(cells):
    ox, oy = (i % COLS) * STRIDE, (i // COLS) * STRIDE
    for y in range(-1, 6):
        for x in range(-1, 6):
            sheet[oy + 1 + y][ox + 1 + x] = pic[min(4, max(0, y))][min(4, max(0, x))]
    entries.append(f"- sprite: {meta(name)}\n  source:\n    x: {ox + 1}\n    y: {oy + 1}\n    width: 5\n    height: 5\n"
                   "  canvas:\n    width: 5\n    height: 5\n    offsetX: 0\n    offsetY: 0\n")
write("StonesSheet.tmp", sheet)
data = (OUT / "StonesSheet.tmp").read_bytes()
(OUT / "StonesSheet.tmp").unlink()
digest = hashlib.sha256(data).hexdigest().upper()
atlas_dir = ROOT / ".monoon-atlas"
atlas_dir.mkdir(exist_ok=True)
for old in atlas_dir.glob("*.png"): old.unlink()   # only this script writes CollisionLab's sheets
(atlas_dir / f"{digest}.png").write_bytes(data)
(OUT / "Stones.atlas").write_text(f"imageHash: {digest}\nentries:\n" + "".join(entries))
meta("Stones.atlas")

# ---- lantern ring: the query circle (radius 38), dashed, 40 art px at 2 units each --------------------------------------
N = 40
ring = []
for y in range(N):
    line = []
    for x in range(N):
        dx, dy = x + .5 - N / 2, y + .5 - N / 2
        d = math.hypot(dx, dy)
        dash = int((math.atan2(dy, dx) + math.pi) / (2 * math.pi) * 24) % 2 == 0
        if 18.7 <= d < 19.7: c = (241, 212, 92, 255) if dash else (225, 163, 32, 150)
        elif d < 18.7: c = (255, 214, 120, int(10 + 22 * (d / 18.7) ** 3))
        else: c = None
        line.append(c)
    ring.append(line)
write("LanternRing.png", ring, 2)
write("BarFill.png", [[hexc("#F1D45C")] * 4] * 4)

# ---- probe: a 20 art px steel sphere, an amber lens on its belt ------------------------------------------------------------
STEEL = ramp("#F0EBCB", "#B8B2CA", "#988CAA", "#726E8D", "#4C4F61", "#353F55")
OUTLINE = hexc("#171429")
probe = []
for y in range(20):
    line = []
    for x in range(20):
        dx, dy = (x + .5 - 10) / 9.6, (y + .5 - 10) / 9.6
        r2 = dx * dx + dy * dy
        if r2 > 1: line.append(None); continue
        if r2 > .80: line.append(OUTLINE); continue
        z = math.sqrt(1 - r2)
        lum = max(0, (-.45 * dx - .55 * dy + .70 * z) / 1.0)   # light from the upper left
        c = STEEL[min(5, max(1, int((1 - lum) * 6)))]
        if math.hypot(dx + .38, dy + .42) < .17: c = STEEL[0]   # specular glint
        if abs(dy - .12) < .1: c = hexc("#353F55") if dy > .12 else hexc("#4C4F61")   # belt
        if abs(dy - .12) < .2 and abs(dx + .05) < .2: c = hexc("#F5E6A5") if dx < -.08 and dy < .12 else hexc("#EFC41F")   # lens
        line.append(c)
    probe.append(line)
write("Probe.png", probe, 2)

# ---- sensor: a 22 art px rune plate, idle (carved) and active (glowing) -------------------------------------------------
def rune(ring_c, mark_c, core_c, fill):
    pic = []
    for y in range(22):
        line = []
        for x in range(22):
            dx, dy = x + .5 - 11, y + .5 - 11
            d, a = math.hypot(dx, dy), math.atan2(dy, dx)
            c = None
            if d < 10.6: c = fill
            if 8.6 <= d < 10.6: c = ring_c
            if 8.6 <= d < 10.6 and int((a + math.pi) / (2 * math.pi) * 16 + .5) % 4 == 0: c = mark_c
            if abs(dx) + abs(dy) < 4.6 and abs(dx) + abs(dy) >= 2.6: c = core_c
            if 5.6 <= d < 6.4: c = mark_c
            line.append(c)
        pic.append(line)
    return pic
write("SensorIdle.png", rune(hexc("#4C4F61"), hexc("#726E8D"), hexc("#726E8D"), (53, 63, 85, 150)), 2)
write("SensorActive.png", rune((92, 214, 222), (214, 252, 246), (240, 255, 250), (60, 150, 170, 120)), 2)

# ---- gates: a white force field (tinted per gate in the scene) and stone posts --------------------------------------------
gate = []
for y in range(45):
    band = (y % 6) in (0, 1)
    gate.append([(240, 235, 203, 235) if x in (0, 4) else (240, 235, 203, 150 if band else 85) for x in range(5)])
write("Gate.png", gate, 2)
post = [[OUTLINE if x in (0, 7) or y in (0, 7) else hexc("#B8B2CA") if y == 1 or x == 1 else hexc("#4C4F61") if y == 6 or x == 6 else hexc("#988CAA")
         for x in range(8)] for y in range(8)]
write("GatePost.png", post, 2)

# ---- polygon wall: KeyRoom's bricks cut to the collider's triangle (0, 0), (1, 0.5), (0, 1) ------------------------------
bw, bh, bricks = load(OUT / "WallFace.png")
brick = lambda x, y: tuple(bricks[(y % (bh // 2)) * 2][(x % (bw // 2)) * 8:(x % (bw // 2)) * 8 + 3])
RW, RH = 50, 60
inside = lambda x, y: 0 <= x < RW and 0 <= y < RH and (x + .5) / RW <= 1 - abs(2 * (y + .5) / RH - 1)
wall = []
for y in range(RH):
    line = []
    for x in range(RW):
        if not inside(x, y): line.append(None); continue
        edge = not all(inside(x + i, y + j) for i, j in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        line.append(OUTLINE if edge else hexc("#B8B2CA") if not inside(x + 1, y - 1) and y < RH / 2 else
                    hexc("#4C4F61") if not inside(x + 1, y + 1) else brick(x, y))
    wall.append(line)
write("RampWall.png", wall, 2)
print("wrote", len(list(OUT.glob("*.png"))), "pictures, atlas", digest[:12])
