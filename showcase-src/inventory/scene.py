# Writes InventoryLab's showcase scene (Scenes/Showcase.scene): a herb yard in the KeyRoom style.
# Run after art.sh:  python3 showcase-src/inventory/scene.py
#
# The sample's own packages run it (InventoryContainer, QuestLog, DialoguePlayer with Herbalist.data, WorkbenchSave);
# new showcase scripts: HerbYard (HUD, talk, slot pick and use, save / crash / load, C to the stock scene), HerbPickup (with a
# pooled "+1" Text 2D from Assets/Labels/DamageNumber.prefab), YardWalker, HeroAnimation (KeyRoom copy).
# World Text 2D: the herbalist's name tag (translation key label.herbalist) and the stall sign (rich text), each on a plate.
# Units as in KeyRoom: one art pixel = 2 world units, camera zoom 1.5, so the fixed camera shows 533 × 300.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/InventoryLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("6a7b735e-336d-5674-b2e7-a136db5a7771")   # id of the retired Inventory.scene, kept as the uuid5 namespace
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
oid = lambda name: uid("obj/" + name)
PROJECT = "ade09cc3-a769-56d7-9832-a2bea14c7e71"
ITEMS, QUESTS, STORY = "27e8e87b-8eac-5142-a8ca-7cf02581b103", "7daa83ca-0ee2-4e06-959b-681c1b7180d5", "be65fc7d-c374-5aa1-8a6b-4f548c4bcd03"
ICONS = "{herb: 5c1a7e2e-3b0d-4f53-9a51-6e0b7c11a503, gem: 589328e1-9fc6-4acb-ac05-ac55da897e12, key: 8b767fbb-02ce-44a7-b503-e1c2cdcaafb1}"
SLOT = "ff9ed6f0-ab54-46ec-ac7f-113d05db0e78"   # Assets/Ui/UiSlot.png
NUMBER = "bc54ecea-34dc-460f-b0a4-3a94f644ac48"  # Assets/Labels/DamageNumber.prefab (40 wide, RisingNumber)
MARKET = "b3de503f-bcdf-46fb-a3f8-f00c841a9807"  # Assets/Market/MarketMorning.wav
STOCK = "09ca087e-0f44-5886-9546-1553180b2fba"   # Scenes/ShowcaseStock.scene
W, H = 533, 300


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def text_asset(name, text):
    (ART / name).write_text(text)
    return asset(name)


def clip(name, frames, seconds):
    return text_asset(name, "frames:\n" + "".join(f"- sprite: {asset(f)}\n  seconds: {seconds:g}\n" for f in frames) + "loop: true\n")


# Hero animator as in KeyRoom: facing sets N, E, S, W, the state clip first.
idle = [clip(f"HeroIdle{d}.clip", [f"HeroIdle{d}.png"], 1) for d in "NESW"]
walk = [clip(f"HeroWalk{d}.clip", [f"HeroWalk{d}{i}.png" for i in (1, 2, 3, 4)], 0.13) for d in "NESW"]
states = lambda name, clips: f"- name: {name}\n  clip: {clips[0]}\n  directions:\n" + "".join(f"  - {c}\n" for c in clips) + "  direction: Move\n"
hero_animator = text_asset("Hero.animator", "entry: Idle\nstates:\n" + states("Idle", idle) + states("Walk", walk)
                           + "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\n"
                           "transitions:\n- from: Idle\n  to: Walk\n  parameter: Speed\n  test: Greater\n  value: 0.1\n- from: Walk\n  to: Idle\n  parameter: Speed\n  test: AtMost\n  value: 0.1\n")
leaves = text_asset("Leaves.particles", 'duration: 0.5\nlooping: false\nrate: 0\nbursts: [{time: 0, count: 14}]\nspread: 360\nspeed: {min: 25, max: 70}\n'
                    'lifetime: {min: 0.3, max: 0.6}\nsize: {min: 3, max: 5}\ncolor: {min: "#9BE27A", max: "#4E9A3C"}\nendSize: 0.3\ngravity: 60\nspace: World\n')

out = []
def obj(name, x, y, w, h, comps="", parent=None, extra=""):
    out.append(f"- id: {oid(name)}\n" + (f"  parentId: {oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def script(name, values="{}"):
    return f"  - type: script\n    script: {name}\n    values: {values}\n"


GROUP = "  - type: sortingGroup2D\n    sortByY: true\n"
SOLID = "  - type: boxCollider2D\n"


def prop(name, image, cx, bottom, solid=True, shadow="ShadowL.png"):
    """A Y-sorted picture standing on `bottom`, centred on cx; solid props block the lower part only."""
    w, h = png_size(image)
    sw, sh = png_size(shadow)
    obj(name, cx - w / 2, bottom - h, w, h, GROUP + sprite(image))
    obj(name + " shadow", (w - sw) / 2, h - sh * .6, sw, sh, sprite(shadow, order=-1), parent=name)
    if solid: obj(name + " base", 4, h - 14, w - 8, 12, SOLID, parent=name)


# ---- yard ----------------------------------------------------------------------------------------------------------
obj("Grass", 0, 0, W, H, f"  - type: spriteRenderer\n    assetId: {asset('Grass.png')}\n    asset: Assets/Showcase/Grass.png\n    drawMode: Tiled\n    order: -20\n")
for i, x in enumerate((24, 92, 160)): prop(f"Fence {i + 1}", "Fence.png", x + 34, 52, shadow="ShadowM.png")
for i, x in enumerate((392, 460, 528)): prop(f"Fence {i + 4}", "Fence.png", x - 34, 52, shadow="ShadowM.png")
prop("Stall", "Stall.png", 292, 128)
prop("Well", "Well.png", 74, 150)
prop("Bush", "Bush.png", 478, 128)
for i, (x, b) in enumerate(((160, 196), (382, 262), (40, 262), (496, 214))): prop(f"Flowers {i + 1}", "Flowers.png", x, b, solid=False, shadow="ShadowM.png")

# Herbalist in front of the counter.
hw, hh = png_size("Herbalist.png")
herbalist = obj("Herbalist", 244, 160 - hh, hw, hh, GROUP + sprite("Herbalist.png"))
sw, sh = png_size("ShadowM.png")
obj("Herbalist shadow", (hw - sw) / 2, hh - sh * .7, sw, sh, sprite("ShadowM.png", order=-1), parent="Herbalist")
obj("Herbalist base", 6, hh - 12, hw - 12, 10, SOLID, parent="Herbalist")

# Player: the feet box (collider, walking, Y-sort) with the animated hero above it, as in KeyRoom.
player = obj("Player", 256, 236, 22, 12, GROUP + script("Showcase/YardWalker") + SOLID)
w, h = png_size("HeroIdleS.png")
obj("Hero shadow", (22 - sw) / 2, 12 - sh * .7, sw, sh, sprite("ShadowM.png", order=-1), parent="Player")
obj("Hero", (22 - w) / 2, 14 - h, w, h, sprite("HeroIdleS.png") + f"  - type: spriteAnimator\n    controller: {hero_animator}\n    playOnStart: true\n"
    + script("Showcase/HeroAnimation"), parent="Player")

# Herbs to pick: three for the request, one more for the save demonstration.
yard = oid("Yard")
for i, (image, cx, bottom) in enumerate((("Herb1.png", 424, 196), ("Herb2.png", 470, 250), ("Herb1.png", 108, 232), ("Herb3.png", 175, 180))):
    w, h = png_size(image)
    obj(f"Herb {i + 1}", cx - w / 2, bottom - h, w, h, GROUP + sprite(image) + "  - type: boxCollider2D\n    isTrigger: true\n"
        + script("Showcase/HerbPickup", f'{{"Yard": "{yard}", "Player": "{player}", "Leaves": "{oid(f"Herb {i + 1} leaves")}", '
                 f'"Pop": {{"Id": "{NUMBER}"}}, "PopX": {w / 2 - 20:g}}}'))
    obj(f"Herb {i + 1} leaves", w / 2, h / 2, 1, 1, f"  - type: particleEmitter2D\n    particles: {leaves}\n    trigger: Manual\n    order: 5\n", parent=f"Herb {i + 1}")

# World labels over a dark plate (the HUD panel frame, sliced), drawn above the Y-sorted yard.
def label(name, cx, y, w, text, size, extra=""):
    obj(name + " plate", cx - w / 2, y, w, size + 6, sprite("Panel.png", sortingLayer=2, drawMode="Sliced", slice="{left: 4, top: 4, right: 4, bottom: 4}"))
    obj(name, cx - w / 2, y, w, size + 6, f'  - type: text2D\n    text: "{text}"\n{extra}    size: {size}\n    color: "#F4F6F4"\n    sortingLayer: 2\n    order: 1\n')
label("Herbalist name", 244 + hw / 2, 160 - hh - 16, 52, "Herbalist", 8, '    textKey: "label.herbalist"\n')
# The sign hangs on the counter front: the HUD tags (top left) and tracker (top right) cover the awning.
label("Stall sign", 322, 102, 52, "<b>Herbs</b> <color=#9BE27A>×3</color>", 8, "    richText: true\n")

obj("Market ambience", 0, 0, 1, 1, f"  - type: audioSource\n    sound: {MARKET}\n    bus: Music\n    volume: 0.35\n    loop: true\n    playOnStart: true\n")
obj("Camera", W / 2, H / 2, 1, 1, "  - type: camera2D\n    zoom: 1.5\n")

# ---- HUD -----------------------------------------------------------------------------------------------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
text = lambda t, size=13, color="#F4F6F4", extra="": f'  - type: uiRect\n  - type: uiText\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n    richText: true\n    icons: {ICONS}\n{extra}'

caption = "A quest, a stacking bag and crash-safe saves"
cw = max(110 + 7.4 * len(caption), 480)   # wide enough for the controls line
cap = obj("Caption", 12, -12, cw, 60, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Canvas")
obj("Caption title", 12, 5, cw - 24, 20, text(f"<b>InventoryLab</b>   {caption}", 15), parent="Caption")
obj("Caption controls", 12, 25, cw - 24, 16, text("Move  ← → ↑ ↓      Talk  Return      Answer  1–4      Stock  C", 12, "#BFD0DC"), parent="Caption")
obj("Caption controls 2", 12, 41, cw - 24, 16, text("Slot  Q  E      Use  U      Save  F5      Load  F9", 12, "#BFD0DC"), parent="Caption")

# Bag: three slots, icon and count (HerbYard writes them and pops a slot when it changes).
bag = obj("Bag", -12, -12, 3 * 50 + 2 * 6 + 16, 66, "  - type: uiRect\n    anchor: BottomRight\n" + panel, parent="Canvas")
slots = [obj(f"Slot {i + 1}", 8 + i * 56, 8, 50, 50, f"  - type: uiRect\n  - type: uiImage\n    sprite: {SLOT}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
             + text("", 13, "#FFFFFF", "    insetX: 5\n    insetY: 10\n")[len("  - type: uiRect\n"):], parent="Bag") for i in range(3)]
mark = obj("Slot mark", 14, 59, 38, 3, '  - type: uiRect\n  - type: uiImage\n    color: "#FFD84A"\n', parent="Bag")   # HerbYard slides it under the selected slot

# Quest tracker (top right) and the status line under it.
tracker = obj("Tracker", -12, 12, 260, 50, "  - type: uiRect\n    anchor: TopRight\n" + panel, parent="Canvas")
obj("Tracker title", 10, 6, 240, 18, text("", 13), parent="Tracker")
obj("Tracker progress", 10, 26, 240, 18, text("", 12, "#BFD0DC"), parent="Tracker")
status = obj("Status", -12, 66, 260, 24, "  - type: uiRect\n    anchor: TopRight\n" + panel + text("", 12, "#FFD84A", "    insetX: 10\n    insetY: 4\n")[len("  - type: uiRect\n"):], parent="Canvas")

# Talk prompt over the herbalist (the camera does not move, so a fixed screen place works).
prompt = obj("Talk prompt", 354, 92, 104, 22, "  - type: uiRect\n" + panel + text("<b>Return</b>  Talk", 12)[len("  - type: uiRect\n"):]
             .replace('    richText: true\n', '    richText: true\n    insetX: 10\n    insetY: 3\n', 1), parent="Canvas", extra="  active: true\n")

# Dialogue box: portrait, name, the typed line and up to four numbered answers.
dlg = obj("Dialogue", 0, -80, 640, 156, "  - type: uiRect\n    anchor: BottomCenter\n" + panel, parent="Canvas")
obj("Portrait", 10, 21, 114, 114, f"  - type: uiRect\n  - type: uiImage\n    sprite: {asset('Portrait.png')}\n", parent="Dialogue")
obj("Speaker", 136, 8, 300, 20, text("<b>Herbalist</b>", 15, "#9BE27A"), parent="Dialogue")
line = obj("Line", 136, 30, 494, 54, text("", 14, "#F4F6F4", "    wrap: true\n    revealRate: 45\n"), parent="Dialogue")
choices = [obj(f"Answer {i + 1}", 136, 86 + i * 17, 494, 17, text("", 13, "#BFD0DC"), parent="Dialogue") for i in range(4)]

# Feature tags (top left), raised by HerbYard as each part is used.
tags = [("talk", "Dialogue graph", "Branching lines from a data asset"), ("quest", "Quest journal", "Counts herbs as you pick them"),
        ("bag", "Stacking bag", "Herbs stack up to 3 in one slot"), ("recovered", "Crash-safe save", "A cut save falls back to the last good copy")]
tag_w = 24 + 6.6 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")

# The sample's packages and the showcase controller live on one object (the layout of the retired Inventory.scene).
ids = lambda xs: "[" + ", ".join(f'"{x}"' for x in xs) + "]"
obj("Yard", 0, 0, 16, 16,
    script("Inventory/InventoryContainer", f'{{"Catalog": {{"Id": "{ITEMS}"}}, "Slots": 3}}')
    + script("Quests/QuestLog", f'{{"Catalog": {{"Id": "{QUESTS}"}}}}')
    + script("Dialogue/DialoguePlayer", f'{{"Story": {{"Id": "{STORY}"}}}}')
    + script("Showcase/HerbYard", "{" + f'"Container": "{yard}", "Quests": "{yard}", "Merchant": "{yard}", "Player": "{player}", "Herbalist": "{herbalist}", "TalkRange": 130, '
             f'"Dialogue": "{dlg}", "Line": "{line}", "Prompt": "{prompt}", "Tracker": "{tracker}", "TrackerTitle": "{oid("Tracker title")}", '
             f'"TrackerProgress": "{oid("Tracker progress")}", "Status": "{status}", "Choices": {ids(choices)}, "Slots": {ids(slots)}, "SlotMark": "{mark}", '
             f'"Stock": {{"Id": "{STOCK}"}}, "ProjectId": "{PROJECT}"' + "}"))

scene = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {oid('Camera')}\n"
(ROOT / "Scenes/Showcase.scene").write_text(scene)
meta = ROOT / "Scenes/Showcase.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/Showcase.scene with {len(out)} objects; scene id {meta.read_text().split()[1]}")
