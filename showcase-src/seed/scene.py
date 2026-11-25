# Writes SeedLab's showcase scenes: Scenes/ShowcaseMaze.scene (a seeded dungeon maze) and Scenes/ShowcaseCrowd.scene
# (the sample's 200-courier crowd in a village yard). Run after art.py:  python3 showcase-src/seed/scene.py
#
# Maze: DungeonMaze (a showcase copy of MazeRun on KeyRoom's 32-unit tiles) rebuilds the dungeon from the seed (N / P),
# walks the explorer one turn per step (Return walks the shortest path to the chest), saves the run (K) and replays it (T).
# Crowd: the sample's CrowdRun unchanged, with its yard geometry (CrowdRun's gate box is fixed in the script) drawn in the
# showcase style; CrowdTags raises the HUD tag flags. Camera zoom 1; one art pixel = 2 world units.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/SeedLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("0cfbf6ac-2359-53aa-abc6-f603bdf4dbee")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
T = 32


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def text_asset(name, text):
    (ART / name).write_text(text)
    return asset(name)


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None, extra=""):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)
    def write(self, file, camera):
        text = "schemaVersion: 42\nobjects:\n" + "".join(self.out) + f"defaultCameraId: {self.oid(camera)}\n"
        (ROOT / "Scenes" / file).write_text(text)
        meta = ROOT / "Scenes" / (file + ".meta")
        if not meta.exists(): meta.write_text(f"id: {uid('scene/' + file)}\n")
        return meta.read_text().split()[1]


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def tiled(image, order=0):
    return sprite(image, drawMode="Tiled", order=order)


def script(name, values="{}"):
    return f"  - type: script\n    script: {name}\n    values: {values}\n"


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
label = lambda t, size=12, color="#F4F6F4", extra="": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n{extra}'


def caption(s, title, controls, width=None):
    w = width or 110 + 7.4 * len(title)
    s.obj("Caption", 12, -12, w, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
    s.obj("Caption title", 12, 5, w - 24, 20, label(f"<b>SeedLab</b>   {title}", 15), parent="Caption")
    s.obj("Caption controls", 12, 25, w - 24, 16, label(controls, 12, "#BFD0DC"), parent="Caption")


# ---- maze ---------------------------------------------------------------------------------------------------------
maze_ts = text_asset("Maze.tileset", f"image: {asset('Maze.png')}\ntileSize: {{width: {T}, height: {T}}}\nmargin: 0\nspacing: 0\nnextId: 6\ntiles:\n"
    + "".join(f"- id: {i + 1}\n  source: {{x: {i * T}, y: 0, width: {T}, height: {T}}}\n  solid: {'true' if i == 0 else 'false'}\n" for i in range(5)))
# Explorer: a 20 × 20 feet box (it collides with the wall tiles) with the KeyRoom hero above it.
hw, hh = png_size("HeroIdleS.png")
sw, sh = png_size("ShadowM.png")
pid = lambda n: uid("prefab/explorer/" + n)
explorer = text_asset("Explorer.prefab", f"""schemaVersion: 42
objects:
- id: {pid('root')}
  name: Explorer
  width: 20
  height: 20
  components:
  - type: boxCollider2D
- id: {pid('shadow')}
  parentId: {pid('root')}
  name: Shadow
  x: {(20 - sw) / 2:g}
  y: {20 - sh * .7:g}
  width: {sw}
  height: {sh}
  components:
{sprite('ShadowM.png', order=3)}- id: {pid('picture')}
  parentId: {pid('root')}
  name: Picture
  groups:
  - Explorer picture
  x: {(20 - hw) / 2:g}
  y: {22 - hh:g}
  width: {hw}
  height: {hh}
  components:
{sprite('HeroIdleS.png', order=5)}""")

m = Scene("maze")
MX, MY = 16, 16                         # the maze is 17 × 11 tiles (8 × 5 cells): 544 × 352
m.obj("Backdrop", 0, 0, 800, 450, '  - type: spriteRenderer\n    squareColor: "#1B1626"\n    order: -30\n')
cw, ch = png_size("Chest.png")
chest = m.obj("Chest", 0, 0, cw, ch, sprite("Chest.png", order=4))
faces = {f"Face{d}": f'{{"Id": "{asset(f"HeroIdle{d}.png")}"}}' for d in "NESW"}
m.obj("Board", MX, MY, T, T, f"  - type: tilemap2D\n    tileset: {maze_ts}\n    cellSize: {{width: {T}, height: {T}}}\n    order: -10\n    chunks: []\n"
      + script("Showcase/DungeonMaze", "{" + f'"Pawn": {{"Id": "{explorer}"}}, "Chest": "{chest}", "Readout": "{m.oid("Seed number")}", "Status": "{m.oid("Status")}", '
               + ", ".join(f'"{k}": {v}' for k, v in faces.items()) + "}"))
m.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
m.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
RX, RW = 576, 212                       # the right column
m.obj("Seed panel", RX, 16, RW, 124, "  - type: uiRect\n" + panel(), parent="Canvas")
m.obj("Seed label", 14, 10, RW - 28, 16, label("SEED", 12, "#BFD0DC"), parent="Seed panel")
m.obj("Seed number", 14, 28, RW - 28, 34, label("104 729", 28, "#FFD84A"), parent="Seed panel")
# A typed seed: SeedEntry hands the field's text to DungeonMaze.ApplySeed.
m.obj("Seed field", 14, 66, 118, 24, '  - type: uiRect\n  - type: uiTextField\n    maxLength: 10\n'
      + '  - type: uiText\n    text: "Type a seed"\n    size: 12\n    color: "#E4ECF2"\n    insetX: 8\n    insetY: 4\n', parent="Seed panel")
m.obj("Seed go", 138, 66, RW - 152, 24, '  - type: uiRect\n  - type: uiButton\n  - type: uiText\n    text: "Go"\n    size: 12\n    color: "#F4F6F4"\n    insetX: 18\n    insetY: 4\n'
      + script("Showcase/SeedEntry", f'{{"Board": "{m.oid("Board")}", "Field": "{m.oid("Seed field")}"}}'), parent="Seed panel")
m.obj("Seed hint", 14, 98, RW - 28, 16, label("N / P   next / previous      R   rebuild", 11, "#BFD0DC"), parent="Seed panel")
m.obj("Status", RX + 4, 148, RW - 8, 36, label("Turn 0     Find the chest.", 12, "#F4F6F4", "    wrap: true\n"), parent="Canvas")
tags = [("seed", "Seeded generation", "Same seed, same dungeon"), ("turns", "Turn-based grid", "One step per turn, walls block"),
        ("replay", "Replay", "Saved turns play back exactly")]
for i, (flag, feature, detail) in enumerate(tags):
    m.obj(f"Tag {i + 1}", RX, 196 + i * 50, RW, 44, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    wrap: true\n    text: "<b>{feature}</b>\\n<color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 5\n', parent="Canvas")
caption(m, "Seeded dungeons and turn replays", "Move  ← → ↑ ↓   Wait  Space   Walk to chest  Return   Save  K   Replay  T / step  J", width=560)
maze_scene = m.write("ShowcaseMaze.scene", "Camera")

# ---- crowd --------------------------------------------------------------------------------------------------------
c = Scene("crowd")
c.obj("Grass", 0, 0, 800, 450, tiled("Grass.png", -20))
c.obj("Start zone", 36, 146, 108, 208, tiled("FloorTile.png", -2))
c.obj("Goal zone", 656, 146, 108, 208, tiled("FloorTile.png", -2))
units, gate, status = c.oid("Couriers"), c.oid("Gate"), c.oid("Arrivals")
c.obj("Yard", 24, 72, 752, 344, "  - {type: navigationRegion2D, spacing: 8, agentWidth: 8}\n"
      + script("CrowdRun", f'{{"Units": "{units}", "Gate": "{gate}", "Status": "{status}"}}') + script("Showcase/CrowdTags"))
for name, x, y, w, h in (("Wall A north", 232, 120, 24, 130), ("Wall A south", 232, 300, 24, 116), ("Wall B", 432, 72, 24, 258),
                         ("Pillar north", 540, 110, 40, 40), ("Pillar south", 540, 230, 40, 40)):
    c.obj(name, x, y, w, h, tiled("WallTile.png", 1) + "  - {type: boxCollider2D}\n")
c.obj("Gate", 232, 250, 24, 50, tiled("Plank.png", 1))
for i, (x, b) in enumerate(((330, 140), (610, 410), (160, 120), (520, 400))): c.obj(f"Flowers {i + 1}", x - 27, b - 32, 54, 32, sprite("Flowers.png", order=0))
c.obj("Couriers", 0, 0, 10, 14, sprite("Villager.png", order=3))
c.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
c.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
c.obj("Arrivals panel", -12, 12, 300, 26, "  - type: uiRect\n    anchor: TopRight\n" + panel(), parent="Canvas")
c.obj("Arrivals", 10, 5, 284, 16, label("Arrived 0 / 200    Gate closed    0.0 s", 12, "#FFD84A"), parent="Arrivals panel")
tags = [("crowd", "Crowd navigation", "200 couriers share one baked area"), ("gate", "Dynamic obstacle", "Opening the gate re-plans everyone")]
tag_w = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    c.obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
caption(c, "A 200-courier crowd finds its way", "Open or close the gate  O")
crowd_scene = c.write("ShowcaseCrowd.scene", "Camera")
print("maze", maze_scene, "crowd", crowd_scene)
