# Writes PlatformerLab's tile-streaming showcase Scenes/ShowcaseStream.scene.  Run:  python3 showcase-src/platformer/stream.py
#
# A ridge seven chunks long (a chunk is 32 x 32 cells) in the Showcase scene's look: its 16-unit Ground autotile, sky, moon and
# hero, camera zoom 3. The ground layer streams (Tilemap 2D → Stream chunks, radius 1, following the runner), so only the
# chunk under the hero and its neighbours are in memory. A minimap camera across the top shows a wider stretch, where terrain
# appears ahead of the hero and is dropped behind him. StreamRunner (Scripts/Showcase) runs the hero along, jumping where the
# ground ahead rises or is missing (it reads that ground with TryGetTile, so only from loaded chunks), and lights one square per
# chunk in the corner panel while IsTileChunkLoaded says it is in memory. Only the tiles stream: the few props sit where their
# chunk is loaded by the time the minimap reaches them.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("3b309c28-75ce-562e-92ac-f94ee48b18c7")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def asset(name, folder="Assets/Showcase"):
    meta = ROOT / folder / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None, extra=""):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, folder="Assets/Showcase", **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image, folder)}\n    asset: {folder}/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('UiShade.png', 'Assets/Ui')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
text = lambda t, size=12, color="#F4F6F4": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n'

# ---- the ridge: a surface row per column (None for a pit), seven chunks of 32 columns ---------------------------------------
C, CHUNK, CHUNKS = 16, 32, 7
W = CHUNK * CHUNKS
surface = []
def run(n, row): surface.extend([row] * n)
run(40, 22)                                                   # chunk 0: the meadow
run(6, 21); run(6, 20); run(10, 20); run(6, 21); run(4, 22)   # steps up and down
run(8, 22); run(3, None); run(10, 21); run(3, None); run(8, 22)   # pits
run(6, 21); run(6, 19); run(10, 19); run(6, 21); run(8, 22)       # a hill
for _ in range(4): run(6, 22); run(2, None)                        # a row of small gaps
run(6, 22); run(5, 21); run(5, 20); run(5, 19); run(20, 19)       # stairs up to a long top
run(W - len(surface), 19)                                          # to the pennant
assert len(surface) == W and all(surface[i] is not None for i in range(0, 8))
END_COL = W - 12
chunks = {}
for x, top in enumerate(surface):
    if top is None: continue
    for y in range(top, CHUNK):
        chunks.setdefault(x // CHUNK, []).append(f"{{index: {y * CHUNK + x % CHUNK}, tile: 1}}")   # tile 1 autotiles its edges

s = Scene("stream")
s.obj("Sky backing", -400, -300, 4600, 1200, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#3E2C7C"\n')
s.obj("Sky", -120, 160, 456, 228, sprite("Sky.png", sortingLayer=-30) + "  - type: parallax2D\n    scrollX: 0.15\n    scrollY: 0\n    repeatX: true\n")
s.obj("Moon", 200, 210, 18, 18, sprite("Moon.png", sortingLayer=-29) + "  - type: parallax2D\n    scrollX: 0.05\n    scrollY: 0\n")
s.obj("Ground", 0, 0, C, C, f"  - type: tilemap2D\n    tileset: {asset('Ground.tileset')}\n    cellSize: {{width: {C}, height: {C}}}\n    collisionLayer: 3\n"
      f"    streamRadius: 1\n    streamTarget: {s.oid('Runner')}\n    order: -1\n    chunks:\n"
      + "".join(f"    - x: {cx}\n      y: 0\n      cells: [{', '.join(cells)}]\n" for cx, cells in sorted(chunks.items())))
end_top = surface[END_COL] * C
s.obj("Goal pennant", END_COL * C + 18, end_top - 29, 20, 29, sprite("Pennant.png", order=1))   # its pole meets the stopped runner's front (the pictures have a few transparent columns)
confetti = s.obj("Confetti", END_COL * C + 28, end_top - 20, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Confetti.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
runner = s.obj("Runner", 3 * C, 22 * C - 30, 22, 30, "  - type: boxCollider2D\n    layer: 1\n  - type: rigidbody2D\n    freezeRotation: true\n"
               f"  - type: particleEmitter2D\n    particles: {asset('LandingDust.particles')}\n    trigger: Landing\n    offsetY: 14\n  - type: characterMotor2D\n")
s.obj("Hero", 0, 0, 21, 30, sprite("HeroIdleE.png", order=4) + f"  - type: spriteAnimator\n    controller: {asset('Hero.animator')}\n    playOnStart: true\n"
      "  - type: script\n    script: MotionFacing\n    values: {}\n", parent="Runner")
bounds = f"{{x: 0, y: 120, width: {W * C}, height: {CHUNK * C - 120}}}"
s.obj("Camera", 3 * C, 22 * C - 40, 1, 1, f"  - type: camera2D\n    zoom: 3\n    follow: {runner}\n    halfLife: 0.25\n    bounds: {bounds}\n")
# The minimap: 96 % of the width, the top 22 %; at zoom 1/3 it shows about 4.5 chunks around the hero.
MX, MY, MW, MH = .02, .03, .96, .22
s.obj("Map Camera", 3 * C, 22 * C - 40, 1, 1, f"  - type: camera2D\n    output: Minimap\n    zoom: 0.333\n    follow: {runner}\n    halfLife: 0.25\n"
      f"    bounds: {bounds}\n    viewport: {{x: {MX}, y: {MY}, width: {MW}, height: {MH}}}\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
px, py, pw, ph, e = MX * 800, MY * 450, MW * 800, MH * 450, 2
for i, (x, y, w, h) in enumerate(((px - e, py - e, pw + 2 * e, e), (px - e, py + ph, pw + 2 * e, e), (px - e, py, e, ph), (px + pw, py, e, ph))):
    s.obj(f"Map frame {i + 1}", x, y, w, h, '  - type: uiRect\n  - type: uiImage\n    color: "#D8B848"\n', parent="Canvas")
tags = [("stream", "Stream chunks", "only the hero's chunk and its neighbours load"), ("minimap", "Minimap camera", "ground appears ahead, drops behind")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, py + ph + 10 + i * 28, tw, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
# Corner panel: the count and one square per chunk, lit while that chunk is in memory.
s.obj("Chunk panel", -12, -12, 214, 84, "  - type: uiRect\n    anchor: BottomRight\n" + panel(), parent="Canvas")
s.obj("Chunk label", 12, 8, 190, 14, text("CHUNKS IN MEMORY", 11, "#BFD0DC"), parent="Chunk panel")
count = s.obj("Chunk count", 12, 22, 190, 30, text(f"0 / {CHUNKS}", 24, "#FFD84A"), parent="Chunk panel")
lit = []
for i in range(CHUNKS):
    s.obj(f"Chunk slot {i + 1}", 12 + i * 27, 58, 23, 12, '  - type: uiRect\n  - type: uiImage\n    color: "#3A3560"\n', parent="Chunk panel")
    lit.append(s.obj(f"Chunk lit {i + 1}", 12 + i * 27, 58, 23, 12, '  - type: uiRect\n  - type: uiImage\n    color: "#FFD84A"\n', parent="Chunk panel"))
s.obj("Caption", 12, -12, 400, 30, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 5, 380, 20, text("<b>PlatformerLab</b>   A long ridge, streamed in chunks", 15), parent="Caption")
values = {"Ground": f'"{s.oid("Ground")}"', "Count": f'"{count}"', "Confetti": f'"{confetti}"', "Lit": "[" + ", ".join(f'"{l}"' for l in lit) + "]",
          "EndX": str(END_COL * C)}
runner_block = next(i for i, o in enumerate(s.out) if "\n  name: Runner\n" in o)
s.out[runner_block] += "  - type: script\n    script: Showcase/StreamRunner\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n"
(ROOT / "Scenes/ShowcaseStream.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseStream.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseStream.scene')}\n")
scene_id = meta.read_text().split()[1]
gs = (ROOT / "GameSettings").read_text()
if scene_id not in gs:   # PlatformerLab writes the list inline: includedScenes: [a, b, ...]
    (ROOT / "GameSettings").write_text(gs.replace("includedScenes: [", f"includedScenes: [{scene_id}, ", 1))
print("scene", scene_id, "chunks", sorted(chunks), "end x", END_COL * C)
