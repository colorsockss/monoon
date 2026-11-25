# Writes DisplayLab's showcase: copies KeyRoom's showcase art into Assets/Showcase and writes Scenes/ShowcaseDisplay.scene.
#   python3 showcase-src/display/scene.py
#
# A torch-lit KeyRoom corner where the hero paces (so motion shows the frame rate), and a display panel: the sample's
# DisplayOption toggles for borderless fullscreen and VSync, FrameMeter's frames-per-second readout, the 12 × 12 pixel badge
# and the click counter. Recorded from the standalone window (Save & Run): Session.Display is null in Scene Play.
import pathlib, shutil, struct, uuid

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
ROOT = SAMPLES / "DisplayLab"
ART = ROOT / "Assets/Showcase"
ART.mkdir(parents=True, exist_ok=True)
for name in ["Floor", "WallCap", "WallFace", "Rug", "Chest", "Barrel", "Pot", "Crate", "TorchSconce", "TorchFlame1", "TorchFlame2", "TorchFlame3",
             "ShadowM"] + [f"HeroWalk{d}{i}" for d in "EW" for i in range(1, 5)]:
    shutil.copy(SAMPLES / f"KeyRoom/Assets/Showcase/{name}.png", ART / f"{name}.png")
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", ART / "Panel.png")
NS = uuid.UUID("4f859b0d-8c5d-5ec4-a090-05dfeeb20078")
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
    def obj(self, name, x, y, w, h, comps="", parent=None):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
label = lambda t, size=13, color="#F4F6F4": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n'
ref = lambda n: f'{{"Id": "{asset(n)}"}}'

s = Scene("display")
RW = 520                                         # the room; the panel takes the right side
s.obj("Backdrop", 0, 0, 800, 450, '  - type: spriteRenderer\n    squareColor: "#15121C"\n    order: -40\n')
s.obj("Floor", 0, 96, RW, 354, sprite("Floor.png", drawMode="Tiled", order=-30))
s.obj("Wall face", 0, 32, RW, 64, sprite("WallFace.png", drawMode="Tiled", order=-25))
s.obj("Wall cap", 0, 0, RW, 32, sprite("WallCap.png", drawMode="Tiled", order=-24))
for i, x in enumerate((372, 468)):   # right of the tags
    s.obj(f"Sconce {i + 1}", x, 52, 32, 22, sprite("TorchSconce.png", order=-20))
    s.obj(f"Flame {i + 1}", x, 26, 32, 32, sprite("TorchFlame1.png", order=-19))
rw, rh = png_size("Rug.png")
s.obj("Rug", 236, 250, rw * 2, rh * 2, sprite("Rug.png", order=-15))
for name, image, x, y in (("Chest", "Chest.png", 440, 140), ("Barrel", "Barrel.png", 30, 120), ("Pot", "Pot.png", 74, 128), ("Crate", "Crate.png", 460, 360)):
    w, h = png_size(image)
    s.obj(name, x, y, w, h, sprite(image, order=y + h))
s.obj("Hero shadow", 84, 374, 32, 10, sprite("ShadowM.png", order=380))
s.obj("Hero", 79, 320, 42, 60, sprite("HeroWalkE1.png", order=381))
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")

PX, PW = 532, 256
s.obj("Display panel", PX, 12, PW, 426, "  - type: uiRect\n" + panel(), parent="Canvas")
s.obj("Panel title", 14, 10, 200, 24, label("<b>Display</b>", 20), parent="Display panel")
s.obj("Badge", PW - 14 - 24, 14, 24, 24, f"  - type: uiRect\n  - type: uiImage\n    sprite: {asset('PixelBadge.png', 'Assets')}\n", parent="Display panel")
s.obj("Badge note", 14, 36, PW - 28, 16, label("12 × 12 badge: sharp at any scale", 11, "#BFD0DC"), parent="Display panel")
s.obj("Fullscreen label", 14, 70, 160, 20, label("Borderless fullscreen"), parent="Display panel")
hint = s.oid("Hint")
s.obj("Fullscreen", PW - 14 - 40, 66, 40, 26, "  - type: uiRect\n  - type: uiToggle\n  - type: script\n    script: DisplayOption\n"
      f'    values: {{"ControlsVSync": false, "Hint": "{hint}"}}\n', parent="Display panel")
s.obj("VSync label", 14, 106, 160, 20, label("Vertical sync"), parent="Display panel")
s.obj("VSync", PW - 14 - 40, 102, 40, 26, "  - type: uiRect\n  - type: uiToggle\n  - type: script\n    script: DisplayOption\n"
      f'    values: {{"ControlsVSync": true, "Hint": "{hint}"}}\n', parent="Display panel")
s.obj("Rate title", 14, 146, 200, 16, label("FRAMES PER SECOND", 11, "#BFD0DC"), parent="Display panel")
rate = s.obj("Rate", 14, 162, PW - 28, 40, label("60", 34, "#FFD84A"), parent="Display panel")
detail = s.obj("Rate detail", 14, 204, PW - 28, 16, label("16.7 ms a frame", 12, "#BFD0DC"), parent="Display panel")
s.obj("Click title", 14, 240, 200, 16, label("CLICK INSIDE THE BUTTON", 11, "#BFD0DC"), parent="Display panel")
s.obj("Click counter", 14, 258, PW - 28, 34, "  - type: uiRect\n  - type: uiButton\n" + '  - type: uiText\n    text: "Clicks: 0"\n    size: 16\n    insetX: 12\n    insetY: 6\n'
      "  - type: script\n    script: ClickCounter\n    values: {}\n", parent="Display panel")
s.obj("Hint", 14, 300, PW - 28, 36, label("Use Save & Run to change desktop settings.", 12, "#FF9F8A") + "    wrap: true\n", parent="Display panel")
s.obj("Window note", 14, 346, PW - 28, 70, label("The game keeps its 800 × 450 picture: a bigger window scales it by whole pixels and fills the rest with bars.", 12, "#BFD0DC") + "    wrap: true\n", parent="Display panel")
tags = [("window", "Any window size", "Whole-pixel scale, bars"), ("fullscreen", "Borderless fullscreen", "Back to the same window"),
        ("vsync", "VSync", "Display rate or uncapped")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail_text) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 8 + i * 28, tw, 24, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail_text}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
s.obj("Caption", 12, -12, 300, 26, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 4, 280, 18, label("<b>DisplayLab</b>   Desktop window settings", 13), parent="Caption")
walk = {f"Walk{d}{i}": ref(f"HeroWalk{d}{i}.png") for d in "EW" for i in range(1, 5)}
flames = {f"Flame{i}": ref(f"TorchFlame{i}.png") for i in range(1, 4)}
values = {"Hero": f'"{s.oid("Hero")}"', "Shadow": f'"{s.oid("Hero shadow")}"', "Rate": f'"{rate}"', "Detail": f'"{detail}"',
          "Flames": f'["{s.oid("Flame 1")}", "{s.oid("Flame 2")}"]'} | walk | flames
s.obj("Meter", 0, 0, 1, 1, "  - type: script\n    script: Showcase/FrameMeter\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseDisplay.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseDisplay.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseDisplay.scene')}\n")
print("scene", meta.read_text().split()[1])
