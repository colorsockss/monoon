# Writes SpriteLab's showcase scene Scenes/ShowcaseYard.scene. Run after art.py:  python3 showcase-src/sprite/scene.py
#
# SpriteTour (Scripts/Showcase) walks the hero round a loop: behind the well and in front of it (both sort by foot Y), along
# the pond bank where a flipped, tinted, ripple-masked copy is its reflection. One 96 × 64 sign PNG is drawn Sliced at three
# sizes, the lantern swings and the coins pulse from property-key clips, and a firefly leaves a Trail 2D.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/SpriteLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("4394934e-c0de-4350-a585-27a682237b83")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None, extra=""):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n{extra}" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"

s = Scene("yard")
s.obj("Grass", 0, 0, 800, 450, sprite("Grass.png", drawMode="Tiled", order=-30))
s.obj("Pond", 430, 328, 330, 100, sprite("Pond.png", order=-20))
s.obj("Pond rim", 430, 328, 330, 100, sprite("PondRim.png", order=-14))   # above the reflection (-15)
s.obj("Well", 300, 150, 66, 80, sprite("Well.png", sortByY="true"))
s.obj("Bush", 40, 112, 50, 42, sprite("Bush.png", sortByY="true"))
s.obj("Flowers", 380, 372, 54, 32, sprite("Flowers.png", order=-10))
# One 96 × 64 PNG, three sizes: corners stay crisp, edges and the middle stretch.
for i, (x, w, h) in enumerate(((16, 96, 56), (122, 160, 56), (292, 232, 76))):
    s.obj(f"Sign {i + 1}", x, 12, w, h, sprite("SignBoard.png", drawMode="Sliced", slice="{left: 16, top: 16, right: 16, bottom: 16}", order=1))
    s.obj(f"Sign {i + 1} text", 0, h / 2 - 12, w, 24, f'  - type: text2D\n    text: "{w} × {h}"\n    size: 14\n    color: "#3A2414"\n    align: Center\n    order: 2\n', parent=f"Sign {i + 1}")
s.obj("Post", 600, 8, 60, 96, sprite("Post.png", order=1))
s.obj("Lantern", 624, 24, 40, 40, sprite("Lantern.png", order=2) + f"  - type: spriteAnimator\n    clip: {asset('LanternSwing.clip')}\n    playOnStart: true\n",
      extra="  pivotX: 0.5\n  pivotY: 0\n")
# The firefly's blink is a two-frame clip imported from FireflyBlink.aseprite (engine/tests/SpriteLab.Aseprite/aseprite_fixture.py
# writes the file; the build or the editor imports it into FireflyBlink.png and FireflyBlink.clip).
s.obj("Firefly", 700, 120, 10, 10, sprite("Firefly.png", order=6)
      + f"  - type: spriteAnimator\n    clip: {asset('FireflyBlink.clip')}\n    playOnStart: true\n"
      + '  - type: trail2D\n    time: 0.8\n    width: 4\n    startColor: "#FFE08C"\n    endColor: "#6CFFB0"\n    maxPoints: 60\n    minStep: 1.5\n    order: 5\n')
for i, (x, y) in enumerate(((560, 236), (592, 248), (624, 236))):
    s.obj(f"Coin {i + 1}", x, y, 14, 14, sprite("Coin.png", sortByY="true") + f"  - type: spriteAnimator\n    clip: {asset('CoinPulse.clip')}\n    playOnStart: true\n")
s.obj("Hero shadow", 134, 200, 32, 10, sprite("ShadowM.png", order=-5))
s.obj("Hero", 129, 145, 42, 60, sprite("HeroWalkE1.png", sortByY="true"))
s.obj("Reflection", 129, 207, 42, 60, sprite("HeroWalkE1.png", order=-15, flipY="true", tint='"#A6CCF2"', mask=asset("Ripple.png")),
      extra="  active: false\n")   # SpriteTour shows it only on the pond bank
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
# In the order they light up during the tour.
tags = [("slice", "9-slice", "one sign PNG, three sizes"), ("ysort", "Y sort by foot", "behind or in front of the well"),
        ("keys", "Property keys", "lantern swing, coin pulse"), ("trail", "Trail 2D", "the firefly's path"),
        ("blink", "Aseprite import", "the firefly's blink"), ("mask", "Tint, flip, mask", "a rippled reflection")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    # 2 units apart; the last one ends 2 above the caption.
    s.obj(f"Tag {i + 1}", 12, 274 + i * 24, tw, 22, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 3\n', parent="Canvas")
s.obj("Caption", 12, -8, 300, 24, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 3, 280, 18, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>SpriteLab</b>   One sprite renderer, many tricks"\n    size: 13\n', parent="Caption")
ref = lambda n: f'{{"Id": "{asset(n)}"}}'
values = {"Hero": f'"{s.oid("Hero")}"', "Shadow": f'"{s.oid("Hero shadow")}"', "Reflection": f'"{s.oid("Reflection")}"', "Firefly": f'"{s.oid("Firefly")}"'}
values |= {f"Walk{d}{i}": ref(f"HeroWalk{d}{i}.png") for d in "NESW" for i in range(1, 5)}
s.obj("Tour", 0, 0, 1, 1, "  - type: script\n    script: Showcase/SpriteTour\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseYard.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseYard.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseYard.scene')}\n")
print("scene", meta.read_text().split()[1])
