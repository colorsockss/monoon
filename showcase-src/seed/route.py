# Writes SeedLab's terrain-cost showcase: Assets/Showcase/Field.png + Field.tileset (terrain names and move costs per tile)
# and Scenes/ShowcaseRoute.scene.  Run:  python3 showcase-src/seed/route.py
#
# A meadow crossed by a river. The tiles are cut from the sample's own art so the scene matches the Crowd and Maze scenes:
# grass from Grass.png, a dirt road and mud recoloured from it, water from SpriteLab's pond, a plank bridge in Plank.png's
# colours; a hedge layer (hedges.py) shapes itself over the solid hedge cells. RouteCourier (Scripts/Showcase) builds a TileNavigation over the
# field and walks the hero along the cheapest route (road 1, grass 3, mud 8; water and hedges block). Rain turns the north road
# to mud and the hero takes the grass; a bridge is built over the south ford and he turns to it. Every SetTile re-plans.
import pathlib, shutil, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import load, save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
ROOT = SAMPLES / "SeedLab"
ART = ROOT / "Assets/Showcase"
for d in "ENSW":
    for i in range(1, 5): shutil.copy(SAMPLES / f"KeyRoom/Assets/Showcase/HeroWalk{d}{i}.png", ART / f"HeroWalk{d}{i}.png")
shutil.copy(SAMPLES / "KeyRoom/Assets/Showcase/Sparkle.particles", ART / "Sparkle.particles")
(ART / "Rain.particles").write_text("""duration: 1
looping: true
rate: 140
shape: Box
width: 300
height: 4
direction: 100
spread: 4
speed: {min: 230, max: 270}
lifetime: {min: 0.28, max: 0.42}
size: {min: 1, max: 2}
aspect: 5
color: {min: "#9FC4FF", max: "#E2EEFF"}
endAlpha: 0.2
""")
(ART / "Build.particles").write_text("""duration: 0.5
looping: false
rate: 0
bursts: [{time: 0, count: 18}]
shape: Box
width: 28
height: 8
spread: 140
speed: {min: 20, max: 70}
lifetime: {min: 0.4, max: 0.8}
size: {min: 2, max: 4}
color: {min: "#8A5A3A", max: "#E0B080"}
endSize: 0.4
gravity: 60
""")
NS = uuid.UUID("0cfbf6ac-2359-53aa-abc6-f603bdf4dbee")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
T = 32


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


# ---- tiles, each 32 x 32, from the sample's own pictures ------------------------------------------------------------------
_, _, GRASS = load(ART / "Grass.png")
_, _, POND = load(SAMPLES / "SpriteLab/Assets/Showcase/Pond.png")
_, _, PLANK = load(ART / "Plank.png")
_, _, STONE = load(SAMPLES / "CardLab/Assets/Showcase/Stone.png")
px = lambda rows, x, y: tuple(rows[y][x * 4:x * 4 + 4])
crop = lambda rows, x0, y0: [[px(rows, x0 + x, y0 + y) for x in range(T)] for y in range(T)]


def recolour(tile, ramp):
    """Keep the texture, swap the palette: each pixel takes the ramp colour at its brightness."""
    lo = min(sum(p[:3]) for row in tile for p in row); hi = max(sum(p[:3]) for row in tile for p in row)
    return [[(*ramp[min(len(ramp) - 1, (sum(p[:3]) - lo) * len(ramp) // max(1, hi - lo + 1))], 255) for p in row] for row in tile]


def stamp(tile, rows, x0, y0):
    for y, line in enumerate(rows):
        for x in range(len(line) // 4):
            p = px(rows, x, y)
            if p[3] and 0 <= x0 + x < T and 0 <= y0 + y < T: tile[y0 + y][x0 + x] = p


grass = [crop(GRASS, x, y) for x, y in ((0, 0), (64, 32), (128, 96), (192, 160))]   # four variants, all cost 3
ROADRAMP = [(150, 112, 70), (176, 136, 88), (196, 160, 108), (214, 182, 130)]
road = [recolour(g, ROADRAMP) for g in grass[:2]]
stamp(road[0], STONE, 4, 9)
mud = recolour(grass[2], [(70, 48, 34), (92, 64, 44), (110, 80, 56), (128, 96, 68)])
for x, y in ((8, 10), (9, 10), (10, 10), (9, 11), (20, 21), (21, 21), (22, 21), (21, 22)):   # two puddles
    mud[y][x] = (96, 140, 190, 255)
water = crop(POND, 60, 30)
shades = sorted({px(PLANK, x, y)[:3] for x in range(24) for y in range(24) if px(PLANK, x, y)[3]}, key=sum)
dark, mid, light = shades[0], shades[len(shades) // 2], shades[-2]
bridge = [row[:] for row in water]
for y in range(2, 30):
    for x in range(T):
        board = x % 8
        bridge[y][x] = (*(dark if board == 7 or y in (2, 29) else light if board == 1 else mid), 255)
hedge = grass[3]
# id: (name, picture, cost, solid). Ids 1-6 are the ones RouteCourier names; 7-10 are grass and road variants.
tiles = {1: ("grass", grass[0], 3, False), 2: ("road", road[0], 1, False), 3: ("mud", mud, 8, False), 4: ("water", water, None, True),
         5: ("bridge", bridge, 1, False), 6: ("hedge", hedge, None, True), 7: ("grass", grass[1], 3, False), 8: ("grass", grass[2], 3, False),
         9: ("grass", grass[3], 3, False), 10: ("road", road[1], 1, False)}
sheet = [bytearray(b"".join(bytes(tiles[i][1][y][x]) for i in sorted(tiles) for x in range(T))) for y in range(T)]
save(ART / "Field.png", T * len(tiles), T, sheet)
(ART / "Field.tileset").write_text(f"image: {asset('Field.png')}\ntileSize: {{width: {T}, height: {T}}}\nmargin: 0\nspacing: 0\nnextId: {len(tiles) + 1}\ntiles:\n"
    + "".join(f"- id: {i}\n  source: {{x: {(i - 1) * T}, y: 0, width: {T}, height: {T}}}\n  solid: {'true' if solid else 'false'}\n  terrain: {name}\n"
              + (f"  moveCost: {cost}\n" if cost else "") for i, (name, _, cost, solid) in sorted(tiles.items())))
tileset = asset("Field.tileset")

# ---- the map: H hedge (a bush stands on it), ~ water, = road, . grass. Start (1,5); the chest is at (16,5) and the hero stops beside it. RouteCourier's rain and
# bridge cells refer to this layout. -----------------------------------------------------------------------------------------
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
NORTH_BRIDGE = [(8, 1), (9, 1)]
cells = []
for y, line in enumerate(MAP):
    for x, c in enumerate(line):
        h = (x * 7 + y * 13) % 4
        tile = {"H": 6, "~": 4, "=": 2 if h < 2 else 10, ".": [1, 7, 8, 9][h]}[c]
        if (x, y) in NORTH_BRIDGE: tile = 5
        cells.append(f"{{index: {y * 32 + x}, tile: {tile}}}")


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None, extra=""):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
text = lambda t, size=12, color="#F4F6F4": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n'

s = Scene("route")
s.obj("Backdrop", -200, -200, 1000, 700, '  - type: spriteRenderer\n    squareColor: "#1B1626"\n    order: -30\n')
s.obj("Field", 0, 0, T, T, f"  - type: tilemap2D\n    tileset: {tileset}\n    cellSize: {{width: {T}, height: {T}}}\n    order: -10\n"
      f"    chunks:\n    - x: 0\n      y: 0\n      cells: [{', '.join(cells)}]\n")
# Hedges and decoration come from Hedges.tileset (hedges.py): every H cell gets the hedge brush, which the engine resolves to
# one of 47 blob shapes from its 8 neighbours (and a random berry variant for the full shape). Flower tufts are a random brush,
# the two torches an animated tile. The Field's own hedge tiles stay underneath for collision and navigation.
hedges = asset("Hedges.tileset")
layer = lambda name, order, cells: s.obj(name, 0, 0, T, T, f"  - type: tilemap2D\n    tileset: {hedges}\n    cellSize: {{width: {T}, height: {T}}}\n"
                                          f"    order: {order}\n    chunks:\n    - x: 0\n      y: 0\n      cells: [{', '.join(f'{{index: {y * 32 + x}, tile: {t}}}' for (x, y), t in cells)}]\n")
layer("Hedges", -9, [((x, y), 1) for y, line in enumerate(MAP) for x, c in enumerate(line) if c == "H"])
# Ripples drift over the river except where the bridges lie (row 1) or are laid (the south ford, row 5).
layer("Decor", -8, [((x, y), 51) for x, y in ((4, 2), (12, 6), (5, 7), (13, 2))] + [((16, 4), 57), ((1, 6), 57)]
      + [((x, y), 61) for y in (0, 2, 3, 4, 6, 7, 8) for x in (8, 9)])
plan = s.obj("Plan", 0, 0, 1, 1, '  - type: line2D\n    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0}\n    width: 5\n    startColor: "#FFE08C"\n    endColor: "#FF9F6B"\n    order: -4\n')
chest = s.obj("Goal chest", 16 * T, 5 * T - 4, 32, 32, sprite("Chest.png", sortByY="true"))
spark = s.obj("Goal spark", 16 * T + 16, 5 * T + 10, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Sparkle.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
courier = s.obj("Courier", 1 * T + 16 - 21, 5 * T + 16 - 48, 42, 60, sprite("HeroIdleE.png", sortByY="true"))
s.obj("Shadow", 5, 52, 32, 10, sprite("ShadowM.png", order=-1), parent="Courier")
rain = s.obj("Rain", 5 * T, -12, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Rain.particles')}\n    trigger: Start\n    sortingLayer: 5\n", extra="  active: false\n")
build = s.obj("Build dust", 0, 0, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Build.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
s.obj("Camera", W * T / 2, H * T / 2 + 12, 1, 1, "  - type: camera2D\n    zoom: 1.25\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
# One big number, as the Maze scene's SEED panel does.
s.obj("Cost panel", -12, 12, 170, 80, "  - type: uiRect\n    anchor: TopRight\n" + panel(), parent="Canvas")
s.obj("Cost label", 12, 8, 150, 14, text("ROUTE COST", 11, "#BFD0DC"), parent="Cost panel")
number = s.obj("Cost number", 12, 22, 150, 32, text("22", 28, "#FFD84A"), parent="Cost panel")
note = s.obj("Cost note", 12, 56, 150, 16, text("Road all the way", 11, "#BFD0DC"), parent="Cost panel")
tags = [("cost", "Terrain costs", "the cheapest route, not the shortest"), ("rain", "Runtime SetTile", "rain turns the road to mud"),
        ("bridge", "New bridge", "a cheaper way opens mid-walk"), ("arrived", "Autotile hedges", "one brush, 47 shapes")]
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 28, 300, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
s.obj("Caption", 12, -12, 470, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption title", 12, 5, 450, 20, text("<b>SeedLab</b>   Grid paths that weigh the ground", 15), parent="Caption")
s.obj("Caption costs", 12, 25, 450, 16, text("Road 1    Grass 3    Mud 8    Bridge 1    Water and hedges block", 12, "#BFD0DC"), parent="Caption")
ref = lambda n: f'{{"Id": "{asset(n)}"}}'
values = {"Courier": f'"{courier}"', "Chest": f'"{chest}"', "Spark": f'"{spark}"', "Plan": f'"{plan}"', "Number": f'"{number}"', "Note": f'"{note}"',
          "Rain": f'"{rain}"', "Build": f'"{build}"'} | {f"Idle{d}": ref(f"HeroIdle{d}.png") for d in "ENSW"} \
         | {f"Walk{d}{i}": ref(f"HeroWalk{d}{i}.png") for d in "ENSW" for i in range(1, 5)}
# The script sits on the tilemap object, so its ObjectId is the board it edits.
field = next(i for i, o in enumerate(s.out) if "\n  name: Field\n" in o)
s.out[field] += "  - type: script\n    script: Showcase/RouteCourier\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n"
(ROOT / "Scenes/ShowcaseRoute.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseRoute.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseRoute.scene')}\n")
print("scene", meta.read_text().split()[1])
