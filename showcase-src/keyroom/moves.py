# Writes KeyRoom's input showcase Scenes/ShowcaseMoves.scene and adds its actions to GameSettings.
#   python3 showcase-src/keyroom/moves.py
#
# The Showcase room (walls, torches, barrels, pots, rug, door) with the door, key, chest and HUD logic taken out. Three actions
# read tick.Input.Triggered with input interactions from GameSettings, and each one does something you can see: Charge (hold
# J 30 ticks) fills a meter over the hero and smashes the crate in front of him, Flick (tap U, up within 12 ticks) throws a
# coin at the training dummy, Dash (double-tap O within 15 ticks) dashes at the dummy and knocks it back. A press that misses
# the rule says why in a word over the hero's head. K rebinds Dash at run time (Session.BeginRebind); the next key becomes the
# dash key. MoveTrainer (Scripts/Showcase) runs it.
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


# ---- the room, from the Showcase scene ------------------------------------------------------------------------------------
src = (ROOT / "Scenes/Showcase.scene").read_text()
body = src.split("objects:\n", 1)[1].split("defaultCameraId:")[0]
blocks = ["- id: " + b for b in body.split("- id: ")[1:]]
name_of = lambda b: re.search(r"\n  name: (.*)", b).group(1)
id_of = lambda b: re.match(r"- id: (\S+)", b).group(1)
drop = {"Exit", "Key", "Chest", "Chest shadow", "Door Camera", "Door dust", "Door feedback", "Chest feedback", "Interface",
        "Crate", "Crate shadow"}   # the crate by the door becomes the breakable crate in front of the hero
gone = set()
for b in blocks:
    parent = re.search(r"\n  parentId: (\S+)", b)
    if name_of(b) in drop or (parent and parent.group(1) in gone): gone.add(id_of(b))
scripts = re.compile(r"  - type: script\n    script: \S+\n    values: \{[^\n]*\}\n")
out = []
for b in blocks:
    if id_of(b) in gone: continue
    b = scripts.sub("", b)                                              # no door lock, movement or hero animation scripts
    b = re.sub(r"  - type: audioSource\n(    .*\n)*", "", b)
    if name_of(b) == "Player": b = re.sub(r"\n  x: \S+\n  y: \S+", "\n  x: 170\n  y: 200", b, 1)
    out.append(b)
ids = {name_of(b): id_of(b) for b in out}
assert all(k in ids for k in ("Player", "Hero", "Main Camera", "Floor"))
panel_id = re.search(r"sprite: (\S+)\n    borders: \{left: 8", src).group(1)
panel = f"  - type: uiImage\n    sprite: {panel_id}\n    borders: {{left: 8, top: 8, right: 8, bottom: 8}}\n"


def oid(name): return uid(f"obj/moves/{name}")
def obj(name, x, y, w, h, comps="", parent=None, extra=""):
    pid = ids.get(parent) or (oid(parent) if parent else None)
    out.append(f"- id: {oid(name)}\n" + (f"  parentId: {pid}\n" if pid else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def burst(particles):
    return f"  - type: particleEmitter2D\n    particles: {asset(particles)}\n    trigger: manual\n    sortingLayer: 5\n    order: 3\n"


off = "  active: false\n"
# The hero faces east and gets a dash trail in place of his animator (the animator only turns him while walking, so
# MoveTrainer sets his sprite itself); a hold meter and a one-word label float over his head.
hero = ids["Hero"]
trail = '  - type: trail2D\n    time: 0.3\n    width: 14\n    startColor: "#9FE6FF"\n    endColor: "#3B5BFF"\n    maxPoints: 30\n    minStep: 2\n    order: -1\n'
out = [re.sub(r"  - type: spriteAnimator\n(    .*\n)*", trail, b, 1).replace("HeroIdleS.png", "HeroIdleE.png").replace(asset("HeroIdleS.png"), asset("HeroIdleE.png"))
       if id_of(b) == hero else b for b in out]
meter_back = obj("Meter back", -10, -60, 42, 6, '  - type: spriteRenderer\n    squareColor: "#1B1626"\n    sortingLayer: 5\n', parent="Player", extra=off)
meter = obj("Meter fill", 1, 1, 40, 4, '  - type: spriteRenderer\n    squareColor: "#FFD84A"\n    sortingLayer: 5\n    order: 1\n', parent="Meter back")
label = obj("Label", 11, -80, 1, 1, '  - type: text2D\n    text: ""\n    richText: true\n    size: 11\n    color: "#FFFFFF"\n    sortingLayer: 5\n', parent="Player")
# The breakable crate, the training dummy and the coin.
cw, ch = png_size("Crate.png")
crate_shadow = obj("Target crate shadow", 228, 198, 44, 12, sprite("ShadowM.png", order=-1))
crate = obj("Target crate", 232, 172, cw, ch, sprite("Crate.png", sortByY="true"))
crate_dust = obj("Crate dust", 250, 190, 1, 1, burst("CrateBits.particles"))   # wood chips that fall
crate_spark = obj("Crate spark", 250, 186, 1, 1, burst("Sparkle.particles"))
dw, dh = png_size("Dummy.png")
obj("Dummy shadow", 437, 212, 44, 12, sprite("ShadowM.png", order=-1))
dummy = obj("Dummy", 432, 216 - dh, dw, dh, sprite("Dummy.png", sortByY="true"))
dummy_spark = obj("Dummy spark", 459, 180, 1, 1, burst("Sparkle.particles"))
coin = obj("Coin", 0, 0, 14, 14, sprite("Coin.png", sortingLayer=5), extra=off)
# Interface: caption, two feature tags, and a rebind prompt shown while the "rebinding" flag is up.
canvas = obj("Interface", 0, 0, 1, 1, "  - type: uiCanvas\n")
obj("Caption", 12, -12, 470, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Interface")
obj("Caption title", 12, 5, 450, 20, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>KeyRoom</b>   Hold, tap and double-tap, set in GameSettings"\n    size: 15\n', parent="Caption")
controls = obj("Controls", 12, 25, 450, 16, '  - type: uiRect\n  - type: uiText\n    text: "Charge  hold J      Flick  tap U      Dash  O O      Rebind  K"\n    size: 12\n    color: "#BFD0DC"\n', parent="Caption")
tags = [("interact", "Input interactions", "hold, tap, double-tap"), ("rebind", "Rebinding", "change a key while it runs")]
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", -12, 12 + i * 28, 236, 24, f"  - type: uiRect\n    anchor: TopRight\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Interface")
obj("Rebind prompt", 0, 96, 240, 34, "  - type: uiRect\n    anchor: TopCenter\n    showIf: rebinding\n" + panel, parent="Interface")
prompt = obj("Rebind text", 14, 8, 220, 20, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "Dash: press a new key"\n    size: 14\n', parent="Rebind prompt")
values = {"Player": ids["Player"], "Hero": hero, "Crate": crate, "CrateShadow": crate_shadow, "CrateDust": crate_dust, "CrateSpark": crate_spark,
          "Dummy": dummy, "DummySpark": dummy_spark, "Coin": coin, "MeterBack": meter_back, "Meter": meter, "Label": label,
          "Prompt": prompt, "Controls": controls}
ref = lambda n: f'{{"Id": "{asset(n)}"}}'
sprites = {"IdleE": ref("HeroIdleE.png")} | {f"Walk{d}{i}": ref(f"HeroWalk{d}{i}.png") for d in "EW" for i in range(1, 5)}
obj("Trainer", 0, 0, 1, 1, "  - type: script\n    script: Showcase/MoveTrainer\n    values: {"
    + ", ".join([f'"{k}": "{v}"' for k, v in values.items()] + [f'"{k}": {v}' for k, v in sprites.items()]) + "}\n")
camera = ids["Main Camera"]
(ROOT / "Scenes/ShowcaseMoves.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {camera}\n")
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
print("scene", scene_id, len(out), "objects")
