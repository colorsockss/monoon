# Writes KeyRoomTiles' showcase: four .tileset files and Scenes/ShowcaseGrids.scene. Run after art.py:
#   python3 showcase-src/tiles/scene.py
#
# Four islands, one per Tilemap 2D layout (Orthogonal, Isometric, HexPointy, HexFlat), each ringed by animated water. GridPainter
# (Scripts/Showcase) lays a road across each with SetTile, one cell at a time, so the terrain brush reconnects the neighbours,
# lifts one road cell and puts it back, then walks a villager along every road.
import pathlib, struct, uuid
from collections import deque

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/KeyRoomTiles"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("53942bb3-17a4-4234-820c-a0754953740a")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
COLUMNS = 8


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


# layout key, engine layout, cell W, H, brush edges, label
GRIDS = [("Square", "Orthogonal", 32, 32, 4, "Square"), ("Iso", "Isometric", 48, 24, 4, "Isometric"),
         ("HexPointy", "HexPointy", 36, 42, 6, "Hex, pointy"), ("HexFlat", "HexFlat", 42, 36, 6, "Hex, flat")]
NEIGHBOURS = {4: [(0, -1), (1, 0), (0, 1), (-1, 0)], 6: [(1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1)]}


def origin(layout, q, r, W, H):
    """Top-left of a cell's bounding box relative to the layer (TileGrid.CellOrigin)."""
    return {"Orthogonal": (q * W, r * H), "Isometric": ((q - r) * .5 * W, (q + r) * .5 * H),
            "HexPointy": ((q + r * .5) * W, r * .75 * H), "HexFlat": (q * .75 * W, (r + q * .5) * H)}[layout]


def cells_for(key):
    if key == "Square": return [(x, y) for y in range(5) for x in range(11)]
    if key == "Iso": return [(q, r) for r in range(6) for q in range(6)]
    if key == "HexPointy": return [(q + 2 - r // 2, r) for r in range(4) for q in range(7)]
    return [(q, r + 4 - q // 2) for q in range(8) for r in range(4)]


def tileset(key, W, H, edges):
    count = 6 + (1 << edges)
    lines = [f"image: {asset(key + '.png')}\ntileSize: {{width: {W}, height: {H}}}\nmargin: 0\nspacing: 0\nnextId: {count + 1}\ntiles:\n"]
    for i in range(count):
        extra = {0: "  randomTiles: 3\n  randomSeed: 11\n", 3: "  frames: 3\n  fps: 3\n", 6: f"  autoEdges: {edges}\n"}.get(i, "")
        lines.append(f"- id: {i + 1}\n  source: {{x: {i % COLUMNS * W}, y: {i // COLUMNS * H}, width: {W}, height: {H}}}\n"
                     f"  solid: {'true' if i in (3, 4, 5) else 'false'}\n{extra}")
    (ART / f"{key}.tileset").write_text("".join(lines))
    return asset(f"{key}.tileset")


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


def road_plan(key, cells, edges):
    """Interior cells; a shortest road from the left-most to the right-most middle cell, then a short branch."""
    W, H = next((g[2], g[3]) for g in GRIDS if g[0] == key)
    layout = next(g[1] for g in GRIDS if g[0] == key)
    cellset = set(cells)
    near = lambda c: [(c[0] + dx, c[1] + dy) for dx, dy in NEIGHBOURS[edges]]
    interior = {c for c in cells if all(n in cellset for n in near(c))}
    centre = lambda c: (origin(layout, *c, W, H)[0] + W / 2, origin(layout, *c, W, H)[1] + H / 2)
    ys = sorted(centre(c)[1] for c in interior)
    mid = ys[len(ys) // 2]
    row = sorted((c for c in interior if abs(centre(c)[1] - mid) <= H * .5), key=lambda c: centre(c)[0])
    start, goal = row[0], row[-1]
    prev, queue = {start: None}, deque([start])
    while queue:
        c = queue.popleft()
        for n in near(c):
            if n in interior and n not in prev: prev[n] = c; queue.append(n)
    path, c = [], goal
    while c: path.append(c); c = prev[c]
    path.reverse()
    # A branch of up to two cells, as far up or down as the island allows, from the path cell nearest the middle that has room.
    for pivot in sorted(path[1:-1], key=lambda c: abs(path.index(c) - len(path) // 2)):
        branch, c = [], pivot
        for _ in range(2):
            options = [n for n in near(c) if n in interior and n not in path and n not in branch and abs(centre(n)[1] - centre(pivot)[1]) > abs(centre(c)[1] - centre(pivot)[1])]
            if not options: break
            c = max(options, key=lambda n: abs(centre(n)[1] - centre(pivot)[1])); branch.append(c)
        if branch: return path, branch, centre
    return path, [], centre


s = Scene("grids")
s.obj("Backdrop", 0, 0, 800, 450, '  - type: spriteRenderer\n    squareColor: "#1D2A33"\n    order: -30\n')
QUADS = [(0, 36, 188), (400, 36, 188), (0, 226, 194), (400, 226, 194)]   # x, y, height
boards, cursors, walkers, labels, kinds, sizes, paths, branches = [], [], [], [], [], [], [], []
for (key, layout, W, H, edges, title), (qx, qy, qh) in zip(GRIDS, QUADS):
    ts = tileset(key, W, H, edges)
    cells = cells_for(key)
    xs = [origin(layout, q, r, W, H)[0] for q, r in cells]; ys = [origin(layout, q, r, W, H)[1] for q, r in cells]
    bw, bh = max(xs) + W - min(xs), max(ys) + H - min(ys)
    ox, oy = round(qx + (400 - bw) / 2 - min(xs)), round(qy + 24 + (qh - 24 - bh) / 2 - min(ys))
    path, branch, centre = road_plan(key, cells, edges)
    if key == "Iso":   # a straight road along one cell axis with a side road; a shortest left-to-right path would staircase
        path, branch = [(1, 2), (2, 2), (3, 2), (4, 2)], [(2, 3), (2, 4)]
    cellset = set(cells)
    water = {c for c in cells if not all((c[0] + dx, c[1] + dy) in cellset for dx, dy in NEIGHBOURS[edges])}
    chunk = "".join(f"      - {{index: {r * 32 + q}, tile: {4 if (q, r) in water else 1}}}\n" for q, r in sorted(cells, key=lambda c: (c[1], c[0])))
    boards.append(s.obj(f"{title} map", ox, oy, W, H, f"  - type: tilemap2D\n    layout: {layout}\n    tileset: {ts}\n    cellSize: {{width: {W}, height: {H}}}\n"
                        f"    order: -10\n    chunks:\n    - x: 0\n      y: 0\n      cells:\n{chunk}"))
    q0, r0 = path[0]
    cx, cy = origin(layout, q0, r0, W, H)
    cursors.append(s.obj(f"{title} cursor", ox + cx, oy + cy, W, H, sprite(f"Cursor{key}.png", order=5)))
    vw, vh = (2 * v for v in png_size("Villager.png"))   # drawn at twice the art size so it reads on the small cells
    walkers.append(s.obj(f"{title} walker", round(ox + cx + W / 2 - vw / 2), round(oy + cy + H / 2 - vh + 4), vw, vh, sprite("Villager.png", order=8, tint='"#FFD58A"')))
    paths.append(path); branches.append(branch)
    kinds.append(["Orthogonal", "Isometric", "HexPointy", "HexFlat"].index(layout)); sizes += [W, H]
    labels.append((title, W, H, qx, qy))

s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
calls = []
for i, (title, W, H, qx, qy) in enumerate(labels):
    s.obj(f"{title} label", qx + 12, qy + 2, 300, 22, "  - type: uiRect\n" + panel(), parent="Canvas")
    s.obj(f"{title} name", 10, 3, 120, 16, label(f"<b>{title}</b>  <color=#BFD0DC>{W} × {H}</color>", 12), parent=f"{title} label")
    calls.append(s.obj(f"{title} call", 140, 3, 152, 16, label("", 12, "#FFD84A"), parent=f"{title} label"))
tags = [("layouts", "Four grids", "One Tilemap 2D"), ("water", "Animated water", "3 frames, one clock"), ("paint", "Runtime SetTile", "Neighbours reconnect")]
x = 12
for i, (flag, feature, detail) in enumerate(tags):
    w = 24 + 6.2 * (len(feature) + len(detail) + 3)
    s.obj(f"Tag {i + 1}", x, 8, w, 22, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 3\n', parent="Canvas")
    x += w + 8
caption = "<b>KeyRoomTiles</b>   One tilemap, four grid layouts"
s.obj("Caption", 12, -6, 330, 24, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 3, 310, 18, label(caption, 13), parent="Caption")

arr = lambda xs: "[" + ", ".join(str(v) for v in xs) + "]"
ids = lambda xs: "[" + ", ".join(f'"{v}"' for v in xs) + "]"
flat = lambda cells: arr([v for c in cells for v in c])
values = {"Boards": ids(boards), "Cursors": ids(cursors), "Walkers": ids(walkers), "Calls": ids(calls), "Kinds": arr(kinds), "Sizes": arr(sizes)}
values |= {f"Path{i}": flat(p) for i, p in enumerate(paths)} | {f"Branch{i}": flat(b) for i, b in enumerate(branches)}
s.obj("Painter", 0, 0, 1, 1, "  - type: script\n    script: Showcase/GridPainter\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseGrids.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseGrids.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseGrids.scene')}\n")
print("scene", meta.read_text().split()[1], [len(p) for p in paths], [len(b) for b in branches])
