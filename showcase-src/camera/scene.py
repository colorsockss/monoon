# Writes CameraLab's showcase scene (Scenes/Showcase.scene): a fair on the sample's 1600 × 896 map.
# Run after art.py:  python3 showcase-src/camera/scene.py
#
# The sample's split screen stays: the player's camera on the left half, the partner's on the right, a third camera draws
# the whole map as a minimap. Screen effects go off at places instead of keys (FxZone): a powder barrel, an evening square.
# Units as in KeyRoom: one art pixel = 2 world units, cameras at zoom 1.5. New scripts: FieldWalker, PatrolWalker, FxZone,
# HeroAnimation (KeyRoom copy).
import pathlib, random, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/CameraLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("cf624488-489b-5951-a279-fffa22d99f7a")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
oid = lambda name: uid("obj/" + name)
MAP_W, MAP_H, T = 1600, 896, 32
SAMPLES = ROOT.parent


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


def animator(who):
    """KeyRoom's hero animator for Hero or Partner pictures: facing sets N, E, S, W, the state clip first."""
    idle = [clip(f"{who}Idle{d}.clip", [f"{who}Idle{d}.png"], 1) for d in "NESW"]
    walk = [clip(f"{who}Walk{d}.clip", [f"{who}Walk{d}{i}.png" for i in (1, 2, 3, 4)], 0.13) for d in "NESW"]
    states = lambda name, clips: f"- name: {name}\n  clip: {clips[0]}\n  directions:\n" + "".join(f"  - {c}\n" for c in clips) + "  direction: Move\n"
    return text_asset(f"{who}.animator", "entry: Idle\nstates:\n" + states("Idle", idle) + states("Walk", walk)
        + "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\n"
        "transitions:\n- from: Idle\n  to: Walk\n  parameter: Speed\n  test: Greater\n  value: 0.1\n- from: Walk\n  to: Idle\n  parameter: Speed\n  test: AtMost\n  value: 0.1\n")


hero_animator, partner_animator = animator("Hero"), animator("Partner")
flame = text_asset("CampFlame.particles", (SAMPLES / "ParticleLab/Assets/Showcase/CampFlame.particles").read_text())
smoke = text_asset("CampSmoke.particles", (SAMPLES / "ParticleLab/Assets/Showcase/CampSmoke.particles").read_text())
blast = text_asset("Blast.particles", (SAMPLES / "BarrageLab/Assets/Showcase/Explosion.particles").read_text().replace("count: 40", "count: 70").replace("max: 150", "max: 220"))
# KeyRoom's floor sheet: 8 × 8 flagstones of 16 art pixels; the mossy and cracked ones are left out of the plaza.
special = {r * 8 + c + 1 for c, r in ((4, 0), (1, 1), (7, 2), (1, 4), (4, 5), (2, 6), (5, 1), (0, 3), (3, 4), (5, 6))}
floor_ts = text_asset("Floor.tileset", f"image: {asset('Floor.png')}\ntileSize:\n  width: {T}\n  height: {T}\nmargin: 0\nspacing: 0\ntiles:\n"
    + "".join(f"- id: {i}\n  source:\n    x: {(i - 1) % 8 * T}\n    y: {(i - 1) // 8 * T}\n    width: {T}\n    height: {T}\n  solid: false\n" for i in range(1, 65)) + "nextId: 65\n")
plain = [i for i in range(1, 65) if i not in special]

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
TRIGGER = "  - type: boxCollider2D\n    isTrigger: true\n"
props = 0
def prop(image, cx, bottom, comps="", shadow="ShadowL.png", name=None):
    """A Y-sorted picture standing on `bottom`, centred on cx, with a soft shadow."""
    global props
    props += 1
    name = name or f"{image[:-4]} {props}"
    w, h = png_size(image)
    obj(name, cx - w / 2, bottom - h, w, h, GROUP + sprite(image) + comps)
    if shadow:
        sw, sh = png_size(shadow)
        obj(name + " shadow", (w - sw) / 2, h - sh * .6, sw, sh, sprite(shadow, order=-1), parent=name)
    return name


def light(radius, color, intensity=.7):
    return f'  - type: pointLight2D\n    radius: {radius}\n    intensity: {intensity}\n    flicker: 0.1\n    frequency: 1.2\n    color: "{color}"\n'


# ---- ground: grass everywhere, a flagstone plaza in the middle ----------------------------------------------------
obj("Grass", 0, 0, MAP_W, MAP_H, f"  - type: spriteRenderer\n    assetId: {asset('Grass.png')}\n    asset: Assets/Showcase/Grass.png\n    drawMode: Tiled\n    order: -20\n")
rng = random.Random(7)
cells = {(c, r): rng.choice(plain) for c in range(19, 32) for r in range(9, 19)}   # x 608..1024, y 288..608
body = "".join(f"      - index: {r * 32 + c}\n        tile: {t}\n" for (c, r), t in sorted(cells.items(), key=lambda i: (i[0][1], i[0][0])))
obj("Plaza", 0, 0, T, T, f"  - type: tilemap2D\n    tileset: {floor_ts}\n    cellSize:\n      width: {T}\n      height: {T}\n    order: -10\n    chunks:\n    - x: 0\n      y: 0\n      cells:\n{body}")

# ---- the fair -------------------------------------------------------------------------------------------------------
for x in range(40, MAP_W, 68): prop("Fence.png", x, 70, shadow="ShadowM.png")          # the north fence
for cx, b in ((60, 250), (90, 860), (300, 860), (1530, 860), (1560, 330), (470, 140), (1150, 150)): prop("Pine.png", cx, b)
# West camp: tent, campfire, bench.
prop("Tent.png", 170, 330)
camp = prop("Campfire.png", 300, 420, comps=light(120, "#FFA34C", .8), name="Campfire")
cw, ch = png_size("Campfire.png")
obj("Camp smoke", cw / 2, 4, 1, 1, f"  - type: particleEmitter2D\n    particles: {smoke}\n    trigger: Start\n    order: -2\n", parent=camp)
obj("Camp flame", cw / 2, 8, 1, 1, f"  - type: particleEmitter2D\n    particles: {flame}\n    trigger: Start\n    order: 2\n", parent=camp)
prop("Bench.png", 380, 470)
# Plaza: well in the middle, two stalls on the north side, crates and barrels.
prop("Well.png", 816, 470)
prop("Stall.png", 700, 330); prop("Stall.png", 940, 330)
for cx, b, img in ((620, 360, "Crate.png"), (1030, 360, "Barrel.png"), (1060, 370, "Crate.png"), (660, 600, "Chest.png")): prop(img, cx, b)
# The powder barrel south-west of the plaza: walking into it sets off the impact effects.
barrel = prop("Barrel.png", 540, 640, name="Powder barrel")
bw, bh = png_size("Barrel.png")
obj("Powder blast", bw / 2, bh / 2, 1, 1, f"  - type: particleEmitter2D\n    particles: {blast}\n    trigger: Manual\n    order: 6\n", parent=barrel)
# East: the evening square, lanterns and flowers; inside it the picture warms up.
for cx, b in ((1230, 300), (1470, 300), (1230, 640), (1470, 640)): prop("Lantern.png", cx, b, comps=light(110, "#FFC46B", .6), shadow="ShadowM.png")
prop("Bench.png", 1350, 330)
for cx, b in ((1300, 470), (1420, 520), (1340, 600), (460, 760), (900, 760), (1100, 220), (240, 600)): prop("Flowers.png", cx, b, shadow=None)
for cx, b, img in ((380, 700, "Herb1.png"), (760, 720, "Herb2.png"), (1180, 760, "Herb3.png"), (520, 230, "Herb2.png")): prop(img, cx, b, shadow=None)
for cx, b in ((1100, 820), (180, 560)): prop("Bush.png", cx, b)
prop("Herbalist.png", 1350, 470, shadow="ShadowM.png")
# Scattered plants away from the plaza and the walking band (y 470..690), so no screen is bare grass.
for i in range(46):
    cx, b = rng.uniform(40, MAP_W - 40), rng.choice((rng.uniform(140, 460), rng.uniform(700, 880)))
    if 590 < cx < 1040 and b < 620: continue
    prop(rng.choice(("Flowers.png", "Herb1.png", "Herb2.png", "Herb3.png", "Bush.png", "Flowers.png")), cx, b, shadow=None)

# ---- player and partner -----------------------------------------------------------------------------------------------
def walker(name, x, y, who, anim, comps):
    w, h = png_size(f"{who}IdleS.png")
    sw, sh = png_size("ShadowM.png")
    obj(name, x, y, 22, 12, GROUP + comps + "  - type: boxCollider2D\n")
    obj(name + " shadow", (22 - sw) / 2, 12 - sh * .7, sw, sh, sprite("ShadowM.png", order=-1), parent=name)
    return obj(name + " picture", (22 - w) / 2, 14 - h, w, h, sprite(f"{who}IdleS.png") + f"  - type: spriteAnimator\n    controller: {anim}\n    playOnStart: true\n"
               + (script("Showcase/HeroAnimation") if who == "Hero" else ""), parent=name)

player = oid("Player")
walker("Player", 420, 520, "Hero", hero_animator, script("Showcase/FieldWalker"))
waypoints = [obj(f"Patrol {i + 1}", x, y, 1, 1) for i, (x, y) in enumerate(((1060, 560), (1190, 640), (1060, 710), (950, 640)))]
walker("Partner", 1060, 560, "Partner", partner_animator,
       script("Showcase/PatrolWalker", '{"Waypoints": [' + ", ".join(f'"{w}"' for w in waypoints) + f'], "Picture": "{oid("Partner picture")}"}}'))

# Effect zones.
obj("Powder zone", 510, 600, 60, 50, TRIGGER + script("Showcase/FxZone", f'{{"Player": "{player}", "Hide": "{oid("Powder barrel")}", "Burst": "{oid("Powder blast")}", "Kind": "Impact", "Camera": "{oid("Player Camera")}"}}'))
obj("Evening zone", 1180, 250, 360, 420, TRIGGER + script("Showcase/FxZone", f'{{"Player": "{player}", "Kind": "Grade", "Camera": "{oid("Player Camera")}"}}'))

# ---- cameras: the sample's split screen and minimap ----------------------------------------------------------------
def camera(name, x, y, output, zoom, viewport, follow=None, extra=""):
    v = viewport
    obj(name, x, y, 1, 1, f"  - type: camera2D\n    output: {output}\n    zoom: {zoom:g}\n    bounds: {{x: 0, y: 0, width: {MAP_W}, height: {MAP_H}}}\n"
        f"    viewport: {{x: {v[0]:g}, y: {v[1]:g}, width: {v[2]:g}, height: {v[3]:g}}}\n" + (f"    follow: {follow}\n    halfLife: 0.16\n" if follow else "") + extra)
half_w, half_h = 400 / 1.5, 450 / 1.5        # world area each half shows
clamp = lambda v, lo, hi: max(lo, min(hi, v))
# The engine's split-screen pair (camera-split-fx): closer than 260 the two views merge into one on the midpoint,
# farther than 340 they split again; the band between keeps the current state.
camera("Player Camera", clamp(431, half_w / 2, MAP_W - half_w / 2), clamp(526, half_h / 2, MAP_H - half_h / 2), "Primary", 1.5, (0, 0, .5, 1), player,
       f"    splitWith: {oid('Partner Camera')}\n    mergeDistance: 260\n    splitDistance: 340\n    blendSeconds: 0.6\n")
camera("Partner Camera", clamp(1071, half_w / 2, MAP_W - half_w / 2), clamp(566, half_h / 2, MAP_H - half_h / 2), "Extra", 1.5, (.5, 0, .5, 1), oid("Partner"))
MAP_ZOOM = 0.11
mw, mh = MAP_W * MAP_ZOOM, MAP_H * MAP_ZOOM          # minimap size in game pixels
mx, my = 800 - 16 - mw, 16
camera("Map Camera", MAP_W / 2, MAP_H / 2, "Minimap", MAP_ZOOM, (mx / 800, my / 450, mw / 800, mh / 450))

# ---- HUD ----------------------------------------------------------------------------------------------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
obj("Seam", 398, 0, 4, 450, '  - type: uiRect\n  - type: uiImage\n    color: "#171429"\n', parent="Canvas")
# A frame of four thin bars: a filled panel would draw over (and darken) the map camera's picture.
for i, (x, y, w, h) in enumerate(((mx - 3, my - 3, mw + 6, 3), (mx - 3, my + mh, mw + 6, 3), (mx - 3, my, 3, mh), (mx + mw, my, 3, mh))):
    obj(f"Minimap edge {i + 1}", x, y, w, h, '  - type: uiRect\n  - type: uiImage\n    color: "#E8D9A0"\n', parent="Canvas")
obj("Minimap label", mx, my + mh + 6, mw, 16, '  - type: uiRect\n  - type: uiText\n    text: "Map camera"\n    size: 11\n    color: "#F4F6F4"\n', parent="Canvas")
obj("Partner label", 412, 12, 120, 22, "  - type: uiRect\n" + panel + '  - type: uiText\n    richText: true\n    text: "<b>Partner</b>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
caption = "Split screen, minimap, screen effects"
cw = 110 + 7.4 * len(caption)
cap = obj("Caption", 12, -12, cw, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, parent="Canvas")
obj("Caption title", 12, 5, cw - 24, 20, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>CameraLab</b>   {caption}"\n    size: 15\n', parent="Caption")
obj("Caption controls", 12, 25, cw - 24, 16, '  - type: uiRect\n  - type: uiText\n    text: "Move  ← → ↑ ↓"\n    size: 12\n    color: "#BFD0DC"\n', parent="Caption")
tags = [("split", "Split screen", "Two cameras, one per player"), ("map", "Minimap", "A third camera draws the whole map"),
        ("edge", "Camera bounds", "The view stops at the map edge"), ("fx", "Per-camera effects", "Shake and flash hit one view only"),
        ("merge", "Merging split", "Close together, the views join")]
tag_w = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")

# Screen effects on keys for the whole screen (ScreenFxDemo): shake and zoom punch with rumble, flash, fade, colour grade,
# a buzz, a custom cursor, comfort settings saved to player.settings, and freezing game time to step it tick by tick.
fx = obj("Effects panel", -12, -12, 300, 104, "  - type: uiRect\n    anchor: BottomRight\n" + panel, parent="Canvas")
line = lambda name, y, text, color="#BFD0DC": obj(name, 10, y, 280, 15, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{text}"\n    size: 11\n    color: "{color}"\n', parent="Effects panel")
line("Effects keys 1", 6, "<b>Screen effects</b>   1 impact   2 flash   3 fade   4 grade", "#F4F6F4")
line("Effects keys 2", 21, "6 rumble   7 cursor   F freeze   . step")
line("Effects keys 3", 36, "5 reduced shake and flash   8 rumble on or off")
line("Comfort", 54, "Shake 100%   Flash 100%", "#F4F6F4")
line("Output", 69, "Rumble on   Cursor system", "#F4F6F4")
line("Clock", 84, "Running", "#F4F6F4")
cursor = (ROOT / "Assets/Cursor.png.meta").read_text().split()[1]
obj("Screen Effects", 0, 0, 1, 1, script("ScreenFxDemo", f'{{"Comfort": "{oid("Comfort")}", "Output": "{oid("Output")}", "Clock": "{oid("Clock")}", "Cursor": {{"Id": "{cursor}"}}}}'))

obj("Split HUD", 0, 0, 1, 1, script("Showcase/SplitHud", f'{{"Main": "{oid("Player Camera")}", "Partner": "{oid("Partner Camera")}", "Seam": "{oid("Seam")}", "Label": "{oid("Partner label")}"}}'))
scene = "schemaVersion: 43\nobjects:\n" + "".join(out) + f"defaultCameraId: {oid('Player Camera')}\n"
(ROOT / "Scenes/Showcase.scene").write_text(scene)
meta = ROOT / "Scenes/Showcase.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/Showcase.scene with {len(out)} objects; scene id {meta.read_text().split()[1]}; tags {tag_w:.0f} px wide")
