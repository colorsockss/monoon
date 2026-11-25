# Writes ParticleLab's second showcase scene, Scenes/ShowcaseWorkshop.scene: a night workshop of five stations the hero walks
# past, one screen (800 units) each at camera zoom 1, with the camp scene's sky, ground and props at their pixel size.
#   python3 showcase-src/particle/workshop.py      (after scene.py, which writes the camp's assets)
#
# The emitters are the retired lab scenes' own objects, with their ids and settings (workshop-emitters.yaml), moved into the
# stations: emitter shapes, sorting and blending, presets, curves, noise and collision. The plain colour panels of the sorting
# scene become the tent and pine (embers rise between them) and the portal (the multiply shade drifts over it); the crate
# the cinders bounce on becomes a bench. WorkshopTags shows each station's HUD tag; LabControls' Space (or 1) pops the
# manual emitter and the Burst group (the Dust and Hit presets).
import pathlib, re, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/ParticleLab"
ART = ROOT / "Assets/Showcase"
HERE = pathlib.Path(__file__).resolve().parent
NS = uuid.UUID("5c0b0a0e-3f1d-5a55-9d8e-1f2f7c3e9a21")
uid = lambda name: str(uuid.uuid5(NS, "workshop/" + name))
W, GROUND, DY = 800, 386, -30        # station width, top of the ground tiles, lift of the lab layouts onto the ground
STATIONS = ["Modules", "Sorting", "Presets", "Curves", "Rain"]
MANUAL = "178b0a25-c969-516c-adad-00177eea4755"   # Manual pop, from the Modules scene


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


def asset(name):
    return (ART / (name + ".meta")).read_text().split()[1]


out = []
def obj(name, x, y, w, h, comps, parent=None, extra=""):
    out.append(f"- id: {uid('obj/' + name)}\n" + (f"  parentId: {parent}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return uid("obj/" + name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def prop(name, image, cx, order=-2, comps="", ground=GROUND):
    w, h = png_size(image)
    return obj(name, cx - w // 2, ground - h, w, h, sprite(image, order=order) + comps)


def light(radius, color, intensity=0.85, flicker=0.1):
    return f"  - type: pointLight2D\n    radius: {radius}\n    intensity: {intensity}\n    flicker: {flicker}\n    frequency: 1.2\n    color: \"{color}\"\n"


def label(name, text, x, y):
    return obj(f"{name} label", x - 60, y, 120, 16, f'  - type: text2D\n    text: "{text}"\n    size: 12\n    color: "#E4DCFF"\n    align: Center\n    order: 8\n')


# ---- the lab objects, moved into their stations ------------------------------------------------------------------------
lab = (HERE / "workshop-emitters.yaml").read_text()
sections = re.split(r"(?m)^station: (\w+)\n", lab)[1:]
roots = {}
for scene, body in zip(sections[0::2], sections[1::2]):
    dx = STATIONS.index(scene) * W
    for block in re.split(r"(?m)^(?=- id: )", body):
        if not block.startswith("- id: "): continue
        name = re.search(r"\n  name: (.*)", block).group(1)
        if name in ("Back panel", "Front panel", "Lit wall", "Crate"): continue   # replaced by pictures below
        # The curve station's three still emitters form the Curves group: the editor-preview checks compare them with the game.
        if name in ("Bubbles", "Flame", "Pinwheels"): block = block.replace("\n  tag: Particles\n", "\n  tag: Particles\n  groups:\n  - Curves\n", 1)
        if "\n  parentId: " not in block:   # a root: move it into the station
            block = re.sub(r"\n  x: (\S+)", lambda m: f"\n  x: {float(m.group(1)) + dx:g}", block, count=1)
            block = re.sub(r"\n  y: (\S+)", lambda m: f"\n  y: {float(m.group(1)) + DY:g}", block, count=1)
            x, y = (float(re.search(rf"\n  {k}: (\S+)", block).group(1)) for k in "xy")
            roots[name] = (x, y)
        out.append(block)

# ---- backdrop: sky fixed to the screen, the sky picture drifting, ground across all stations ----------------------------
obj("Sky backing", -400, -200, 2000, 900, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#3E2C7C"\n  - type: parallax2D\n    scrollX: 0\n    scrollY: 0\n')
sw, sh = png_size("Sky.png")
obj("Sky", -40, GROUND - sh + 40, sw, sh, sprite("Sky.png", sortingLayer=-30) + "  - type: parallax2D\n    scrollX: 0.15\n    scrollY: 0\n    repeatX: true\n")
ground_ts = asset("Ground.tileset")
cells = {}
for c in range(-2, len(STATIONS) * W // 32 + 2):
    for r in range(2): cells.setdefault(c // 32, []).append(f"{{index: {r * 32 + c % 32}, tile: 1}}")
chunks = "".join(f"    - x: {k}\n      y: 0\n      cells: [{', '.join(v)}]\n" for k, v in sorted(cells.items()))
obj("Ground", 0, GROUND, 32, 32, f"  - type: tilemap2D\n    tileset: {ground_ts}\n    cellSize: {{width: 32, height: 32}}\n    order: -1\n    chunks:\n{chunks}")

# Lanterns between the stations.
for i in range(len(STATIONS) + 1):
    prop(f"Lantern {i + 1}", "Lantern.png", i * W + 16, comps=light(110, "#FFC46B", 0.6))

# Sorting: embers rise between the tent (behind) and a pine (in front); the multiply shade drifts over the glowing portal.
s = W
tw, th = png_size("Tent.png")
obj("Tent", s + 260 - tw // 2, GROUND - th, tw, th, sprite("Tent.png", order=0))
pw, ph = png_size("Pine.png")
obj("Pine", s + 300 - pw // 2, GROUND - ph, pw, ph, sprite("Pine.png", order=2))
qw, qh = png_size("Portal.png")
obj("Portal", s + 630 - qw // 2, 150, qw, qh, sprite("Portal.png", order=0) + light(140, "#B98CFF", 0.7))
# Noise and collision: the cinders bounce on a bench (its box collider).
bw, bh = png_size("Bench.png")
obj("Bench", 4 * W + 600 - bw // 2, GROUND - bh, bw, bh, sprite("Bench.png", order=0) + "  - type: boxCollider2D\n")

# A name under every effect.
names = {"Point sparks": "Point", "Circle ring": "Circle", "Edge rain": "Edge", "Box snow": "Box", "Cone fountain": "Cone",
         "Prewarmed smoke": "Prewarm", "Manual pop": "Manual (Space)", "Local tail": "Local space", "World wake": "World space",
         "Embers between": "Between sprites", "Multiply shade": "Multiply", "Flake sheet": "Sheet frames",
         "Dust": "Dust", "Smoke": "Smoke", "Sparks": "Sparks", "Hit": "Hit", "Rain": "Rain", "Torch": "Torch prefab",
         "Brazier": "Brazier prefab", "Bubbles": "Bubbles", "Flame": "Flame", "Pinwheels": "Pinwheels", "Mist": "Mist (noise)",
         "Cinders": "Cinders (bounce)"}
for name, text in names.items():
    x, y = roots[name]
    label(name, text, x, min(y + 34, GROUND - 18))   # just above the ground, clear of the caption

# ---- hero, camera, HUD -----------------------------------------------------------------------------------------------
hero = obj("Hero", 60, GROUND - 60, 44, 60, "  - type: boxCollider2D\n  - type: rigidbody2D\n    freezeRotation: true\n  - type: characterMotor2D\n"
           "  - type: script\n    script: Walker\n    values: {\"Speed\": 180}\n"
           "  - type: script\n    script: WorkshopTags\n    values: {}\n")
hw, hh = png_size("HeroIdleE.png")
obj("Hero sprite", 1, 0, hw, hh, sprite("HeroIdleE.png", order=4) + f"  - type: spriteAnimator\n    controller: {asset('Hero.animator')}\n    playOnStart: true\n"
    "  - type: script\n    script: MotionFacing\n    values: {}\n", hero)
obj("Camera", 400, 225, 1, 1, f"  - type: camera2D\n    follow: {hero}\n    halfLife: 0.3\n    bounds: {{x: 0, y: 0, width: {len(STATIONS) * W}, height: 450}}\n"
    f"  - type: script\n    script: LabControls\n    values: {{\"Manual\": \"{MANUAL}\"}}\n")

canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
caption = "Emitter modules, sorting, presets and curves"
width = 110 + 7.4 * len(caption)
cap = obj("Caption", 12, -12, width, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, canvas)
obj("Caption title", 12, 5, width - 24, 20, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>ParticleLab</b>   {caption}"\n    size: 15\n', cap)
obj("Caption controls", 12, 25, width - 24, 16, '  - type: uiRect\n  - type: uiText\n    text: "Walk  ← →      Pop and burst  Space"\n    size: 12\n    color: "#BFD0DC"\n', cap)
tags = [("shapes", "Emitter shapes", "point, circle, edge, box, cone; prewarm; local and world space"),
        ("sorting", "Sorting and blending", "between sprites, multiply, sheet frames"),
        ("presets", "Presets", "six built-ins; two prefabs share one asset"),
        ("curves", "Curves and gradients", "size, colour and spin over each life"),
        ("weather", "Noise and collision", "mist drifts on noise, cinders bounce")]
tag_w = 24 + 6.6 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', canvas)

text = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {uid('obj/Camera')}\n"
(ROOT / "Scenes/ShowcaseWorkshop.scene").write_text(text)
meta = ROOT / "Scenes/ShowcaseWorkshop.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/ShowcaseWorkshop.scene with {len(out)} objects, id {meta.read_text().split()[1]}")
