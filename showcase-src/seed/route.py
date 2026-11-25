# Writes SeedLab's terrain-cost showcase: Assets/Showcase/Field.png + Field.tileset (terrain names and move costs per tile)
# and Scenes/ShowcaseRoute.scene.  Run:  python3 showcase-src/seed/route.py
#
# RouteCourier (Scripts/Showcase) builds a TileNavigation over the field and walks a courier along the cheapest route
# (road 1, grass 3, mud 8; water and trees block). Mid-walk, rain turns a stretch of the northern road to mud and later a bridge
# is built across the river with SetTile; every cost change re-plans from the courier's cell. A Line 2D draws the plan.
import pathlib, random, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import save

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/SeedLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("0cfbf6ac-2359-53aa-abc6-f603bdf4dbee")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
T = 32


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


# ---- tiles: 16 × 16 art pixels each, drawn at 2 units per pixel ----------------------------------------------------------
rng = random.Random(4)
def tile(base, speck, density=6, extra=None):
    art = [[base] * 16 for _ in range(16)]
    for _ in range(256 // density):
        x, y = rng.randrange(16), rng.randrange(16)
        art[y][x] = rng.choice(speck)
    if extra: extra(art)
    return art
GRASS = tile((104, 156, 66), [(92, 142, 58), (128, 176, 82)])
ROAD = tile((196, 164, 112), [(176, 142, 94), (214, 186, 136)], 5)
MUD = tile((112, 84, 56), [(92, 68, 46), (136, 104, 70), (80, 60, 40)], 3)
WATER = tile((58, 112, 172), [(84, 140, 200), (50, 98, 156)], 7)
def planks(art):
    for y in range(16):
        for x in range(16):
            art[y][x] = (150, 104, 60) if y % 5 else (96, 64, 36)
            if x in (0, 15): art[y][x] = (82, 54, 32)
BRIDGE = tile((150, 104, 60), [(150, 104, 60)], 99, planks)
def tree(art):
    for y in range(16):
        for x in range(16):
            d = (x - 7.5) ** 2 + (y - 7) ** 2
            if d < 40: art[y][x] = (44, 104, 58) if (x + y) % 5 else (64, 130, 70)
            if d < 40 and y > 9 and abs(x - 7.5) < 1.5: art[y][x] = (96, 64, 36)
TREE = tile((104, 156, 66), [(92, 142, 58)], 9, tree)
tiles = [("grass", GRASS, 3, False), ("road", ROAD, 1, False), ("mud", MUD, 8, False), ("water", WATER, None, True),
         ("bridge", BRIDGE, 1, False), ("tree", TREE, None, True)]
sheet = [sum((list(t[1][y]) for t in tiles), []) for y in range(16)]
save(ART / "Field.png", 16 * len(tiles) * 2, 32, [bytearray(b"".join(bytes((*c, 255)) * 2 for c in line)) for line in sheet for _ in (0, 1)])
(ART / "Field.tileset").write_text(f"image: {asset('Field.png')}\ntileSize: {{width: {T}, height: {T}}}\nmargin: 0\nspacing: 0\nnextId: {len(tiles) + 1}\ntiles:\n"
    + "".join(f"- id: {i + 1}\n  source: {{x: {i * T}, y: 0, width: {T}, height: {T}}}\n  solid: {'true' if solid else 'false'}\n  terrain: {name}\n"
              + (f"  moveCost: {cost}\n" if cost else "") for i, (name, _, cost, solid) in enumerate(tiles)))
tileset = asset("Field.tileset")


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
label = lambda t, size=12, color="#F4F6F4": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n'

BX, BY = 16, 56
s = Scene("route")
s.obj("Backdrop", 0, 0, 800, 450, '  - type: spriteRenderer\n    squareColor: "#1B1626"\n    order: -30\n')
s.obj("Field", BX, BY, T, T, f"  - type: tilemap2D\n    tileset: {tileset}\n    cellSize: {{width: {T}, height: {T}}}\n    order: -10\n    chunks: []\n")
path = s.obj("Plan", 0, 0, 1, 1, '  - type: line2D\n    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0}\n    width: 4\n    startColor: "#FFE08C"\n    endColor: "#FF9F6B"\n    order: 2\n')
cw, ch = 40, 34
chest = s.obj("Goal chest", 0, 0, cw, ch, sprite("Chest.png", order=3))
courier = s.obj("Courier", 0, 0, 20, 28, sprite("Villager.png", order=5, tint='"#FFD58A"'))
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
RX, RW = 672, 116
s.obj("Legend", RX, BY, RW, 132, "  - type: uiRect\n" + panel(), parent="Canvas")
s.obj("Legend title", 10, 8, 100, 14, label("ENTRY COST", 11, "#BFD0DC"), parent="Legend")
for i, (name, cost) in enumerate((("Road", "1"), ("Grass", "3"), ("Mud", "8"), ("Bridge", "1"), ("Water, trees", "-"))):
    s.obj(f"Legend {name}", 10, 26 + i * 20, 96, 16, label(f"{name}   <color=#FFD84A>{cost}</color>", 12), parent="Legend")
status = s.obj("Status", RX, BY + 140, RW, 96, "  - type: uiRect\n" + panel(), parent="Canvas")
status_text = s.obj("Status text", 10, 8, RW - 20, 80, label("", 12, "#F4F6F4") + "    wrap: true\n", parent="Status")
tags = [("cost", "Terrain costs", "the cheapest route, not the shortest"), ("rain", "Runtime SetTile", "rain turns road to mud: re-plan"),
        ("bridge", "New bridge", "a cheaper way opens mid-walk")]
tw = 24 + 5.6 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):   # two on the first row, one under the first
    s.obj(f"Tag {i + 1}", 12 + (tw + 6) * (i % 2), 6 if i < 2 else 30, tw, 22, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 11\n    insetX: 10\n    insetY: 3\n', parent="Canvas")
s.obj("Caption", 12, -8, 360, 24, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 3, 340, 18, label("<b>SeedLab</b>   Grid paths that weigh the ground", 13), parent="Caption")
values = {"Courier": f'"{courier}"', "Chest": f'"{chest}"', "Plan": f'"{path}"', "Status": f'"{status_text}"'}
# The script sits on the tilemap object, so its ObjectId is the board it edits.
field = next(i for i, o in enumerate(s.out) if "\n  name: Field\n" in o)
s.out[field] += "  - type: script\n    script: Showcase/RouteCourier\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n"
(ROOT / "Scenes/ShowcaseRoute.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseRoute.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseRoute.scene')}\n")
print("scene", meta.read_text().split()[1])
