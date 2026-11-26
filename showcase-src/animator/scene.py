# Writes AnimatorLab's showcase scene (Scenes/Showcase.scene): a training yard in the KeyRoom style.
# Run after art.sh:  python3 showcase-src/animator/scene.py
#
# Left, the scout walks with the arrow keys; its animator picks one of 8 Walk clips (4 Idle) from the Move vector.
# Right, Space makes the guard attack a training dummy: the sample's Guard animator (Attack while Combo < 3, Recover and
# back to Idle on exit time, Any State → Hurt on Hit) with new clips; the Swing clip event knocks the dummy, which hits
# back after every third blow. A small state graph (top right) follows the guard's current state.
# New scripts: ScoutWalker, GuardTrainer, StateGraph.
import pathlib, re, struct, uuid, zlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/AnimatorLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("793807d6-c20d-4c32-982f-08aa2e216b01")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
oid = lambda name: uid("obj/" + name)
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


def clip(name, frames, loop=True):
    """frames: (picture, seconds[, event]) tuples."""
    body = "".join(f"- sprite: {asset(f[0])}\n  seconds: {f[1]:g}\n" + (f"  event: {f[2]}\n" if len(f) > 2 else "") for f in frames)
    return text_asset(name, "frames:\n" + body + f"loop: {str(loop).lower()}\n")


# Gold 9-slice frame for the state graph's highlight (2 art px border, clear inside).
def frame_png(name, n=6):
    gold, clear = (255, 216, 74, 255), (0, 0, 0, 0)
    rows = [[gold if x in (0, n - 1) or y in (0, n - 1) else clear for x in range(n) for _ in (0, 1)] for y in range(n) for _ in (0, 1)]
    raw = b"".join(b"\x00" + bytes(c for p in r for c in p) for r in rows)
    chunk = lambda k, d: struct.pack(">I", len(d)) + k + d + struct.pack(">I", zlib.crc32(k + d))
    (ART / name).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", n * 2, n * 2, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    return asset(name)
highlight_png = frame_png("Highlight.png")

# Scout: Idle has 4 direction clips (N E S W), Walk 8 (N NE E SE S SW W NW); the state clip is the first of each set.
idle = [clip(f"ScoutIdle{d}.clip", [(f"HeroIdle{d}.png", 1)]) for d in "NESW"]
walk = [clip(f"ScoutWalk{d}.clip", [(f"HeroWalk{d}{i}.png", .12) for i in (1, 2, 3, 4)]) for d in ("N", "NE", "E", "SE", "S", "SW", "W", "NW")]
dirs = lambda clips: "".join(f"  - {c}\n" for c in clips)
scout_animator = text_asset("Scout.animator", f"entry: Idle\nstates:\n- name: Idle\n  clip: {idle[0]}\n  directions:\n{dirs(idle)}  direction: Move\n"
    f"- name: Walk\n  clip: {walk[0]}\n  directions:\n{dirs(walk)}  direction: Move\n"
    "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\n"
    "transitions:\n- from: Idle\n  to: Walk\n  parameter: Speed\n  test: Greater\n  value: 0.1\n- from: Walk\n  to: Idle\n  parameter: Speed\n  test: AtMost\n  value: 0.1\n")

# Guard: the transitions of the sample's old Guard.animator (removed; its showcase copy is now the source) with the new clips.
g_idle = clip("GuardIdle.clip", [("GuardIdle.png", 1)])
g_walk = clip("GuardWalk.clip", [("GuardWalk1.png", .2), ("GuardWalk2.png", .2)])
g_attack = clip("GuardAttack.clip", [("GuardWindup.png", .16), ("GuardSwing.png", .12, "Swing"), ("GuardFollow.png", .24)], loop=False)
g_recover = clip("GuardRecover.clip", [("GuardIdle.png", .3)], loop=False)
g_hurt = clip("GuardHurt.clip", [("GuardHurt.png", .7, "Ouch")], loop=False)
guard_src = (ART / "Guard.animator").read_text()
for state, new in (("Idle", g_idle), ("Walk", g_walk), ("Attack", g_attack), ("Recover", g_recover), ("Hurt", g_hurt)):
    guard_src = re.sub(rf"(- name: {state}\n  clip: )\S+", rf"\g<1>{new}", guard_src)
guard_animator = text_asset("Guard.animator", guard_src)
chips = text_asset("Chips.particles", 'duration: 0.5\nlooping: false\nrate: 0\nbursts: [{time: 0, count: 18}]\ndirection: -60\nspread: 80\n'
                   'speed: {min: 50, max: 120}\nlifetime: {min: 0.35, max: 0.6}\nsize: {min: 2, max: 4}\ncolor: {min: "#E8C77A", max: "#A9793C"}\n'
                   'gravity: 320\nspace: World\n')

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
n = 0
def prop(image, cx, bottom, comps="", shadow="ShadowL.png", name=None):
    global n
    n += 1
    name = name or f"{image[:-4]} {n}"
    w, h = png_size(image)
    obj(name, cx - w / 2, bottom - h, w, h, GROUP + sprite(image) + comps)
    if shadow:
        sw, sh = png_size(shadow)
        obj(name + " shadow", (w - sw) / 2, h - sh * .6, sw, sh, sprite(shadow, order=-1), parent=name)
    return name


# ---- the yard -----------------------------------------------------------------------------------------------------
obj("Grass", 0, 0, W, H, f"  - type: spriteRenderer\n    assetId: {asset('Grass.png')}\n    asset: Assets/Showcase/Grass.png\n    drawMode: Tiled\n    order: -20\n")
for x in range(24, W + 40, 68): prop("Fence.png", x, 66, shadow="ShadowM.png")
for cx, b, img in ((500, 120, "Barrel.png"), (470, 112, "Crate.png"), (30, 120, "Bush.png"), (510, 290, "Bush.png"), (250, 110, "Well.png")): prop(img, cx, b)
for cx, b, img in ((60, 280, "Flowers.png"), (200, 290, "Herb1.png"), (440, 280, "Flowers.png"), (330, 120, "Herb2.png"), (150, 120, "Flowers.png")): prop(img, cx, b, shadow=None)

# Scout (left): feet box with the animated picture above it, as in KeyRoom.
sw, sh = png_size("ShadowM.png")
w, h = png_size("HeroIdleS.png")
obj("Scout", 110, 185, 22, 12, GROUP + script("Showcase/ScoutWalker", f'{{"Picture": "{oid("Scout picture")}", "Facing": "{oid("Facing text")}"}}'))
obj("Scout shadow", (22 - sw) / 2, 12 - sh * .7, sw, sh, sprite("ShadowM.png", order=-1), parent="Scout")
obj("Scout picture", (22 - w) / 2, 14 - h, w, h, sprite("HeroIdleS.png") + f"  - type: spriteAnimator\n    controller: {scout_animator}\n    playOnStart: true\n", parent="Scout")

# Guard and dummy (right).
dw, dh = png_size("DummyUp.png")
dummy = prop("DummyUp.png", 400, 214, name="Dummy")
obj("Chips", dw / 2, dh * .45, 1, 1, f"  - type: particleEmitter2D\n    particles: {chips}\n    trigger: Manual\n    order: 3\n", parent="Dummy")
gw, gh = png_size("GuardIdle.png")
obj("Guard", 318 - gw / 2, 214 - gh, gw, gh, GROUP + sprite("GuardIdle.png") + f"  - type: spriteAnimator\n    controller: {guard_animator}\n    playOnStart: true\n"
    + script("Showcase/GuardTrainer", f'{{"Dummy": "{oid("Dummy")}", "Chips": "{oid("Chips")}", "DummyUp": {{"Id": "{asset("DummyUp.png")}"}}, '
             f'"DummyBack": {{"Id": "{asset("DummyBack.png")}"}}, "DummyForward": {{"Id": "{asset("DummyForward.png")}"}}}}'))
obj("Guard shadow", (gw - png_size("ShadowL.png")[0]) / 2, gh - png_size("ShadowL.png")[1] * .6, *png_size("ShadowL.png"), sprite("ShadowL.png", order=-1), parent="Guard")

obj("Camera", W / 2, H / 2, 1, 1, "  - type: camera2D\n    zoom: 1.5\n")

# ---- HUD ----------------------------------------------------------------------------------------------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
label = lambda t, size=12, color="#F4F6F4", extra="": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n{extra}'

caption = "Direction sets, combos and clip events"
cw = 110 + 7.4 * len(caption)
obj("Caption", 12, -12, cw, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Canvas")
obj("Caption title", 12, 5, cw - 24, 20, label(f"<b>AnimatorLab</b>   {caption}", 15), parent="Caption")
obj("Caption controls", 12, 25, cw - 24, 16, label("Walk  ← → ↑ ↓      Attack  Space", 12, "#BFD0DC"), parent="Caption")
obj("Facing", 12, -62, 200, 24, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Canvas")
obj("Facing text", 10, 4, 186, 16, label("<b>Scout</b>   Idle  ·  S"), parent="Facing")

# State graph of the guard's animator (top right). Boxes 72 × 24; arrows and conditions as small labels.
GX, GY, GW, GH = 800 - 12 - 318, 12, 318, 152
obj("Graph", GX, GY, GW, GH, "  - type: uiRect\n" + panel, parent="Canvas")
obj("Graph title", 12, 6, 200, 16, label("<b>Guard animator</b>", 12), parent="Graph")
# Walk is left out: the guard only trains on the spot, so the showcase never reaches it (the animator still has it).
boxes = {"Hurt": (124, 30), "Idle": (12, 78), "Attack": (124, 78), "Recover": (234, 78)}
box_ids = []
for name, (x, y) in boxes.items():
    box_ids.append((name, obj(f"State {name}", x, y, 72, 24, "  - type: uiRect\n" + panel + f'  - type: uiText\n    text: "{name}"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Graph")))
for i, (x, y, t) in enumerate(((12, 34, "Any State  →"), (95, 82, "→"), (86, 64, "Attack"), (206, 82, "→"), (204, 64, "exit"), (234, 104, "exit → Idle"), (200, 34, "on Hit"))):
    obj(f"Graph note {i + 1}", x, y, 110, 14, label(t, 10, "#BFD0DC"), parent="Graph")
obj("Graph params", 12, 124, 186, 16, label("State <b>Idle</b>", 11), parent="Graph")
hl = obj("Highlight", boxes["Idle"][0] - 2, boxes["Idle"][1] - 2, 76, 28, f"  - type: uiRect\n  - type: uiImage\n    sprite: {highlight_png}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n", parent="Graph")
names = ",".join(n for n, _ in box_ids)
obj("Graph driver", 0, 0, 1, 1, script("Showcase/StateGraph", f'{{"Animated": "{oid("Guard")}", "Highlight": "{hl}", "Params": "{oid("Graph params")}", '
    f'"StateBoxes": [' + ", ".join(f'"{b}"' for _, b in box_ids) + f'], "StateNames": "{names}"}}'))

tags = [("walk8", "Direction sets", "Walk picks one of 8 clips from Move"), ("combo", "Conditions", "Attack fires only while Combo is under 3"),
        ("event", "Clip events", "The Swing frame knocks the dummy"), ("exit", "Exit time", "Attack → Recover → Idle as clips end"),
        ("hurt", "Any State", "Hit interrupts whatever is playing")]
tag_w = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")

scene = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {oid('Camera')}\n"
(ROOT / "Scenes/Showcase.scene").write_text(scene)
meta = ROOT / "Scenes/Showcase.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/Showcase.scene with {len(out)} objects; scene id {meta.read_text().split()[1]}; tags {tag_w:.0f} px")
