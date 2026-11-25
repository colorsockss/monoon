# Writes PlatformerLab's lighting showcase Scenes/ShowcaseCave.scene. Copies a few KeyRoom / InventoryLab pictures into
# Assets/Showcase first.  Run:  python3 showcase-src/platformer/cave.py
#
# A stone hall. It starts lit; CaveNight (Scripts/Showcase) turns the Global Light 2D on, the hall drops to a dark ambient,
# and the torches (Sprite Shader 2D flames over Point Light 2D with shadows) and the hero's lantern carve the light; pillars,
# barrels and a sliding crate (Position Tween 2D) cast shadows that move. Crystals sit on the unlit layer and keep their glow.
import pathlib, shutil, struct, uuid

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
ROOT = SAMPLES / "PlatformerLab"
ART = ROOT / "Assets/Showcase"
for src, name in (("KeyRoom/Assets/Showcase/WallFace.png", "WallFace.png"), ("KeyRoom/Assets/Showcase/WallCap.png", "WallCap.png"),
                  ("KeyRoom/Assets/Showcase/Crate.png", "Crate.png"), ("KeyRoom/Assets/Showcase/Barrel.png", "Barrel.png"),
                  ("InventoryLab/Assets/Icons/Crystal.png", "Crystal.png")):
    shutil.copy(SAMPLES / src, ART / name)
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

FLOOR = 380
s = Scene("cave")
s.obj("Night", 0, 0, 1, 1, '  - type: globalLight2D\n    color: "#2C3450"\n    unlitLayer: 5\n', extra="  active: false\n")
s.obj("Wall", 0, 0, 800, FLOOR, sprite("WallFace.png", drawMode="Tiled", order=-20))
s.obj("Wall cap", 0, 0, 800, 32, sprite("WallCap.png", drawMode="Tiled", order=-19))
s.obj("Floor", 0, FLOOR, 800, 450 - FLOOR, sprite("Ground.png", drawMode="Tiled", order=-10) + "  - type: boxCollider2D\n")
pw, ph = png_size("Pillar.png")
for i, x in enumerate((236, 548)):
    s.obj(f"Pillar {i + 1}", x, FLOOR - ph + 20, pw // 2, ph - 20, sprite("Pillar.png", order=-5) + "  - type: boxCollider2D\n")
# Torches: a wall post, a shader flame above it, and a shadow-casting light at the flame.
for i, x in enumerate((112, 400, 690)):
    s.obj(f"Torch post {i + 1}", x - 23, 196, 46, 32, sprite("TorchPost.png", order=-4))
    s.obj(f"Torch flame {i + 1}", x - 12, 160, 24, 36, f"  - type: spriteShader2D\n    shader: {asset('Torch.shader', 'Assets')}\n    sortingLayer: 5\n")
    s.obj(f"Torch light {i + 1}", x - 1, 176, 2, 2, light(240, ["#FFB060", "#FFC070", "#FFA050"][i], [.35, .3, .4][i]))
for name, image, x in (("Barrel 1", "Barrel.png", 160), ("Crate 1", "Crate.png", 336), ("Barrel 2", "Barrel.png", 640)):
    w, h = png_size(image)
    s.obj(name, x, FLOOR - h, w, h, sprite(image, order=-3) + "  - type: boxCollider2D\n")
cw, chh = png_size("Crate.png")
s.obj("Sliding crate", 452, FLOOR - chh, cw, chh, sprite("Crate.png", order=-3) + "  - type: boxCollider2D\n  - type: rigidbody2D\n    bodyType: \"Kinematic\"\n"
      "  - type: positionTween2D\n    offsetX: 70\n    offsetY: 0\n    duration: 2.4\n")
for i, x in enumerate((716, 744)):
    s.obj(f"Crystal {i + 1}", x, FLOOR - 28 - i * 4, 28, 28, sprite("Crystal.png", sortingLayer=5))
hw, hh = png_size("HeroWalkE1.png")
s.obj("Hero", 40, FLOOR - hh + 2, hw, hh, sprite("HeroWalkE1.png", order=0))
s.obj("Lantern light", hw / 2 + 8, 24, 2, 2, light(170, "#FFF0C0", .05, 1), parent="Hero")
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
tags = [("shader", "Sprite Shader 2D", "procedural torch flames"), ("dark", "Global Light 2D", "ambient darkness"),
        ("shadow", "Shadows", "colliders block every light"), ("unlit", "Unlit layer", "crystals keep their glow")]   # in the order they light up
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 40 + i * 26, tw, 22, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 3\n', parent="Canvas")
s.obj("Caption", 12, -8, 300, 24, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 3, 280, 18, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>PlatformerLab</b>   2D lights and shadows"\n    size: 13\n', parent="Caption")
ref = lambda n: f'{{"Id": "{asset(n)}"}}'
values = {"Night": f'"{s.oid("Night")}"', "Hero": f'"{s.oid("Hero")}"'} | {f"Walk{d}{i}": ref(f"HeroWalk{d}{i}.png") for d in "EW" for i in range(1, 5)}
s.obj("Show", 0, 0, 1, 1, "  - type: script\n    script: Showcase/CaveNight\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseCave.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseCave.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseCave.scene')}\n")
print("scene", meta.read_text().split()[1])
