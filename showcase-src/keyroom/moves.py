# Writes KeyRoom's input showcase Scenes/ShowcaseMoves.scene and adds its actions to GameSettings.
#   python3 showcase-src/keyroom/moves.py
#
# A training room. Three actions read tick.Input.Triggered with input interactions from GameSettings: Charge (hold J for
# 30 ticks), Flick (tap U, released within 12 ticks), Dash (double-tap O within 15 ticks). MoveTrainer (Scripts/Showcase)
# shows each rule, a live hold meter and a log, and plays the move on the hero and the training dummy. K rebinds Dash at run
# time (Session.BeginRebind): the next key becomes the dash key and the row shows it.
import pathlib, re, shutil, struct, uuid

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
ROOT = SAMPLES / "KeyRoom"
ART = ROOT / "Assets/Showcase"
shutil.copy(SAMPLES / "PlatformerLab/Assets/Ui/UiShade.png", ART / "Panel.png")
shutil.copy(SAMPLES / "AnimatorLab/Assets/Showcase/DummyForward.png", ART / "Dummy.png")
NS = uuid.UUID("64fb4cd6-2469-5cec-ba6d-9a49e61f42e7")
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
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
label = lambda t, size=12, color="#F4F6F4": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n'

s = Scene("moves")
s.obj("Floor", 0, 64, 800, 386, sprite("Floor.png", drawMode="Tiled", order=-30))
s.obj("Wall face", 0, 0, 800, 64, sprite("WallFace.png", drawMode="Tiled", order=-29))
dw, dh = png_size("Dummy.png")
s.obj("Dummy shadow", 610, 300, 32, 10, sprite("ShadowM.png", order=-5))
dummy = s.obj("Dummy", 600, 306 - dh, dw, dh, sprite("Dummy.png", sortByY="true"))
hw, hh = png_size("HeroIdleE.png")
s.obj("Hero shadow", 405, 300, 32, 10, sprite("ShadowM.png", order=-5))
hero = s.obj("Hero", 400, 306 - hh, hw, hh, sprite("HeroIdleE.png", sortByY="true")
             + '  - type: trail2D\n    time: 0.35\n    width: 10\n    startColor: "#9FE6FF"\n    endColor: "#3B5BFF"\n    maxPoints: 30\n    minStep: 2\n    order: -1\n')
burst = s.obj("Burst", 0, 0, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Sparkle.particles')}\n    trigger: Manual\n    order: 5\n", parent="Hero")
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
# The rules panel: one row per action (name, rule, the key it is bound to), a hold meter and a log line.
PW = 330
s.obj("Rules", 12, 76, PW, 186, "  - type: uiRect\n" + panel(), parent="Canvas")
s.obj("Rules title", 12, 8, 300, 16, label("INPUT INTERACTIONS", 11, "#BFD0DC"), parent="Rules")
rows = [("Charge", "hold for 30 ticks"), ("Flick", "tap, release within 12 ticks"), ("Dash", "double-tap within 15 ticks")]
keys, names = [], []
for i, (name, rule) in enumerate(rows):
    y = 30 + i * 34
    names.append(s.obj(f"{name} name", 12, y, 80, 18, label(f"<b>{name}</b>", 14), parent="Rules"))
    keys.append(s.obj(f"{name} key", 92, y, 60, 18, label("J", 14, "#FFD84A"), parent="Rules"))
    s.obj(f"{name} rule", 150, y + 2, 170, 16, label(rule, 11, "#BFD0DC"), parent="Rules")
s.obj("Meter back", 12, 136, PW - 24, 10, "  - type: uiRect\n" + panel(), parent="Rules")
meter = s.obj("Meter fill", 2, 2, 1, 6, f"  - type: uiRect\n  - type: uiImage\n    sprite: {asset('Panel.png')}\n    color: \"#FFD84A\"\n", parent="Meter back")
log = s.obj("Log", 12, 154, PW - 24, 28, label("Press J, U or O.", 12, "#9FE6A0") + "    wrap: true\n", parent="Rules")
tags = [("interact", "Hold, tap, double-tap", "input interactions in GameSettings"), ("rebind", "Rebinding", "change a key while the game runs")]
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 270 + i * 28, tw, 24, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
s.obj("Caption", 12, -10, 380, 26, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 4, 360, 18, label("<b>KeyRoom</b>   Charge J    Flick U    Dash O O    Rebind dash K", 13), parent="Caption")
values = {"Hero": f'"{hero}"', "Dummy": f'"{dummy}"', "Burst": f'"{burst}"', "Meter": f'"{meter}"', "Log": f'"{log}"',
          "Keys": "[" + ", ".join(f'"{k}"' for k in keys) + "]", "Names": "[" + ", ".join(f'"{n}"' for n in names) + "]"}
s.obj("Trainer", 0, 0, 1, 1, "  - type: script\n    script: Showcase/MoveTrainer\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseMoves.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseMoves.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseMoves.scene')}\n")
scene_id = meta.read_text().split()[1]

# GameSettings: the three actions with their interactions, and the scene in the build.
gs = (ROOT / "GameSettings").read_text()
if "name: Charge" not in gs:
    gs = gs.replace("  - name: KeyBindings\n", "  - {name: Charge, controls: [J], interaction: Hold, ticks: 30}\n  - {name: Flick, controls: [U], interaction: Tap, ticks: 12}\n"
                    "  - {name: Dash, controls: [O], interaction: DoubleTap, ticks: 15}\n  - name: KeyBindings\n", 1)
if scene_id not in gs:
    gs = gs.replace("includedScenes:\n", f"includedScenes:\n- {scene_id}\n", 1)
(ROOT / "GameSettings").write_text(gs)
print("scene", scene_id)
