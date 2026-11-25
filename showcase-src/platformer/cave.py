# Writes PlatformerLab's lighting showcase Scenes/ShowcaseCave.scene in the Showcase scene's look (its night sky, moon, ground
# and hero; ground blocks and pillars are cut from Ground.png by blocks.py).  Run:  python3 showcase-src/platformer/cave.py
#
# A ruined gate at dusk. CaveNight (Scripts/Showcase) switches the Global Light 2D on: the world drops to a dark ambient and only
# the hero's lantern shines. Walking east he lights the torches one by one (sparks, then a Sprite Shader 2D flame over a
# shadow-casting Point Light 2D), so the shadows build up torch by torch; then he walks back through them. Pillars, the beam and the
# crates (one sliding on a Position Tween 2D) cast the shadows. Every caster's collider is its whole, fully opaque picture:
# shadows start at the collider's far edges, so a caster with transparent corners would show them lit. The hero has no
# collider. The moon sits on the unlit layer and keeps its glow.
import pathlib, struct, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import blocks

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("3b309c28-75ce-562e-92ac-f94ee48b18c7")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def asset(name, folder="Assets/Showcase"):
    meta = ROOT / folder / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


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


def light(radius, color, flicker=0.0, intensity=1.0):
    return f'  - type: pointLight2D\n    radius: {radius}\n    intensity: {intensity}\n    flicker: {flicker}\n    frequency: 1.7\n    color: "{color}"\n    shadows: true\n'


panel = lambda: f"  - type: uiImage\n    sprite: {asset('UiShade.png', 'Assets/Ui')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
box = "  - type: boxCollider2D\n"
off = "  active: false\n"

G = 250                      # ground top; the camera (zoom 1.5) shows 533 x 300 units
s = Scene("cave")
s.obj("Night", 0, 0, 1, 1, '  - type: globalLight2D\n    color: "#2A2C50"\n    unlitLayer: 5\n', extra=off)
s.obj("Sky backing", -100, -100, 800, 500, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#3E2C7C"\n')
s.obj("Sky", -190, G + 30 - 456, 912, 456, sprite("Sky.png", sortingLayer=-30))
s.obj("Moon", 430, 34, 36, 36, sprite("Moon.png", sortingLayer=5))
s.obj("Ground", -30, G, 600, 60, sprite(blocks.block(600, 60), order=-5) + box)
# The gate: two rock pillars under a beam.
for i, x in enumerate((150, 340)):
    s.obj(f"Pillar {i + 1}", x, G - 150, 44, 150, sprite(blocks.block(44, 150), order=-5) + box)
bw, bh = png_size("Beam.png")
s.obj("Beam", 151, G - 150 - bh, bw, bh, sprite("Beam.png", order=-4) + box)
# Torches: a pole, the Showcase scene's bracket, a shader flame and a light that starts off.
pole = blocks.squeeze("DeckLeg.png", "Pole12x90.png", 12, 90)
TORCHES = (110, 269, 430)   # left of the gate, under the beam, right of the gate; CaveNight lights each as the hero passes
(ART / "Ignite.particles").write_text("""duration: 0.6
looping: false
rate: 0
bursts: [{time: 0, count: 30}]
spread: 120
speed: {min: 40, max: 130}
lifetime: {min: 0.3, max: 0.7}
size: {min: 1.5, max: 3}
color: {min: "#FF8A3A", max: "#FFE9A0"}
endSize: 0.3
gravity: 160
blend: Additive
""")
for i, x in enumerate(TORCHES):
    s.obj(f"Torch pole {i + 1}", x - 6, G - 90, 12, 90, sprite(pole, order=-6))
    s.obj(f"Torch post {i + 1}", x - 23, G - 100, 46, 32, sprite("TorchPost.png", order=-4))
    s.obj(f"Torch flame {i + 1}", x - 12, G - 134, 24, 36, f"  - type: spriteShader2D\n    shader: {asset('Torch.shader', 'Assets')}\n    sortingLayer: 5\n", extra=off)
    s.obj(f"Torch spark {i + 1}", x, G - 112, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Ignite.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
    s.obj(f"Torch light {i + 1}", x - 1, G - 118, 2, 2, light(200, ["#FFB060", "#FFC070", "#FFA050"][i], [.3, .25, .35][i], 1), extra=off)
cw, ch = png_size("Crate.png")
for name, x, y in (("Crate 1", 56, G - ch), ("Crate 2", 466, G - ch), ("Crate 3", 474, G - 2 * ch)):
    s.obj(name, x, y, cw, ch, sprite("Crate.png", order=-3) + box)
s.obj("Sliding crate", 212, G - ch, cw, ch, sprite("Crate.png", order=-3) + box + '  - type: rigidbody2D\n    bodyType: "Kinematic"\n'
      "  - type: positionTween2D\n    offsetX: 70\n    offsetY: 0\n    duration: 2.4\n")
pw, ph = png_size("Pennant.png")
s.obj("Pennant", 247 - pw / 2 + 20, G - 150 - bh - ph, pw, ph, sprite("Pennant.png", order=-4))   # a flag on the gate's beam
hw, hh = png_size("HeroWalkE1.png")
s.obj("Hero", 20, G - hh, hw, hh, sprite("HeroWalkE1.png", order=0))
s.obj("Lantern light", hw / 2 + 8, 24, 2, 2, light(160, "#FFF0C0", .05, 1), parent="Hero", extra=off)
s.obj("Camera", 266.5, 150, 1, 1, "  - type: camera2D\n    zoom: 1.5\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
tags = [("shader", "Sprite Shader 2D", "procedural torch flames"), ("dark", "Global Light 2D", "dusk falls over the ruins"),
        ("shadow", "Shadows", "pillars, beam and crates block light"), ("unlit", "Unlit layer", "the moon keeps its glow")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 28, tw, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
s.obj("Caption", 12, -12, 330, 30, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 5, 310, 20, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>PlatformerLab</b>   2D lights and shadows"\n    size: 15\n', parent="Caption")
ref = lambda n: f'{{"Id": "{asset(n)}"}}'
ids = lambda name: "[" + ", ".join(f'"{s.oid(f"{name} {i + 1}")}"' for i in range(len(TORCHES))) + "]"
values = {"Night": f'"{s.oid("Night")}"', "Hero": f'"{s.oid("Hero")}"', "Lantern": f'"{s.oid("Lantern light")}"',
          "Flames": ids("Torch flame"), "Lights": ids("Torch light"), "Sparks": ids("Torch spark")} \
         | {f"Walk{d}{i}": ref(f"HeroWalk{d}{i}.png") for d in "EW" for i in range(1, 5)}
s.obj("Show", 0, 0, 1, 1, "  - type: script\n    script: Showcase/CaveNight\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseCave.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseCave.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseCave.scene')}\n")
print("scene", meta.read_text().split()[1])
