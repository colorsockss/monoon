# Writes CollisionLab's second showcase, Scenes/ShowcaseCasts.scene: the collision lab's probe course in a torch-lit corridor.
#   python3 showcase-src/collision/casts.py     (after art.py)
#
# Art: KeyRoom's flagstones, brick wall and torches (Point Light 2D, the middle one casts shadows of the gates and the wall)
# under a Global Light 2D; the probe is a steel sphere with its own small light, the sensor a rune plate that lights up
# (SensorEntry swaps in SensorActive), the gates tinted force fields between stone posts, the wall KeyRoom's bricks cut to
# the triangle. Names and the ray draw on the unlit layer. Decoration has no colliders, so the measured geometry is unchanged.
#
# The objects keep the lab's ids, names, positions and colliders, so the engine checks that measure them (CollisionChecks,
# CollisionLab.InspectorEdits) see the same geometry: a probe circle (r 20) at (80, 180), a sensor ring (trigger), two gates on
# layers outside the probe's reach (A is not in the probe's mask; B's own mask is empty) and a solid triangle on the wall layer.
# Probe casts a ray and sweeps its circle toward the triangle on the first step and draws both (the line to the hit, a ghost
# where the circle stops); → rolls the probe at 320 units per second through the sensor and both gates, against the triangle.
import pathlib, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/CollisionLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("5750e5bb-1700-5a5b-94c2-b6bd02a0356c")
uid = lambda name: str(uuid.uuid5(NS, "casts/" + name))
asset = lambda path: (ROOT / (path + ".meta")).read_text().split()[1]
# The lab's object ids (Probe's script fields and the checks name them).
PROBE, SENSOR, PASS_A, PASS_B, RAMP = ("f6bf5031-579f-560b-9bdc-9b9f007cc814", "91298410-d245-500d-93af-4d10a6465374",
                                      "34f2f966-38e7-5e2a-978b-9cd5047cd054", "9ce8a341-74b7-5cf9-aec6-69306e76ff71",
                                      "f13ad8d5-3899-5e4c-a37e-a52f69661de0")
CAMERA, CANVAS, METRICS, STATUS = ("aa364bf6-24b5-5dfe-9bde-191b0c9c94c9", "38c04c97-d92c-5d23-be0a-5111ea8522ba",
                                   "ad0a8179-0349-5cbd-bd6c-acd40cb22956", "7f5e0b61-85f9-556e-ad9e-189b6c3a009d")

out = []
def obj(name, x, y, w, h, comps="", parent=None, oid=None, extra=""):
    oid = oid or uid("obj/" + name)
    out.append(f"- id: {oid}\n" + (f"  parentId: {parent}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return oid


def sprite(path, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(path)}\n    asset: {path}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Assets/Showcase/Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
text = lambda t, size=12, color="#F4F6F4", extra="": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n{extra}'

TORCH = '  - type: pointLight2D\n    radius: 170\n    intensity: 0.8\n    flicker: 0.1\n    frequency: {f}\n    color: "#FF8A3A"\n{shadows}'
obj("Night", 0, 0, 1, 1, '  - type: globalLight2D\n    color: "#4A4E7E"\n    unlitLayer: 5\n')
obj("Backing", 0, 0, 800, 450, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#171429"\n')
obj("Floor", 0, 144, 800, 236, sprite("Assets/Showcase/Floor.png", drawMode="Tiled", sortingLayer=-10))
obj("Wall cap", 0, 48, 800, 32, sprite("Assets/Showcase/WallCap.png", drawMode="Tiled", sortingLayer=-9))
obj("Wall", 0, 80, 800, 64, sprite("Assets/Showcase/WallFace.png", drawMode="Tiled", sortingLayer=-9))
obj("Wall cap low", 0, 380, 800, 32, sprite("Assets/Showcase/WallCap.png", drawMode="Tiled", sortingLayer=-9))
for i, (x, f) in enumerate(((120, 1.1), (450, 1.4), (720, 1.25))):
    torch = obj(f"Torch {i + 1}", x, 110, 32, 22, sprite("Assets/Showcase/TorchSconce.png", sortingLayer=5)
                + TORCH.format(f=f, shadows="    shadows: true\n" if i == 1 else ""))
    obj(f"Torch {i + 1} flame", 0, -32, 32, 32, sprite("Assets/Showcase/TorchFlame1.png", sortingLayer=5, order=1)
        + f"  - type: spriteAnimator\n    clip: {asset('Assets/Showcase/Flame.clip')}\n    playOnStart: true\n", parent=torch)
ray = obj("Ray", 0, 0, 1, 1, '  - type: line2D\n    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0}\n    width: 2\n    startColor: "#FFE08C"\n    endColor: "#FF9F6B"\n    sortingLayer: 5\n    order: 4\n')
ghost = obj("Sweep stop", 0, 0, 40, 40, sprite("Assets/Showcase/Probe.png", tint='"#9A98C8"', order=3), extra="  active: false\n")
obj("Probe", 80, 180, 40, 40, sprite("Assets/Showcase/Probe.png", order=10) + "  - type: circleCollider2D\n    layer: 1\n    mask: 44\n"
    "  - type: script\n    script: Probe\n    values: {" + f'"Status": "{STATUS}", "Metrics": "{METRICS}", "Wall": "{RAMP}", "Sensor": "{SENSOR}", "RayLine": "{ray}", "SweepStop": "{ghost}"' + "}\n"
    '  - type: pointLight2D\n    radius: 56\n    intensity: 0.45\n    color: "#FFD087"\n', oid=PROBE)
obj("Probe shadow", 4, 34, 32, 10, sprite("Assets/Showcase/ShadowM.png", order=-1), parent=PROBE)
obj("Sensor", 218, 178, 44, 44, sprite("Assets/Showcase/SensorIdle.png", order=2) + "  - type: circleCollider2D\n    layer: 3\n    mask: 2\n    isTrigger: true\n"
    f'  - type: script\n    script: SensorEntry\n    values: {{"Active": {{"Id": "{asset("Assets/Showcase/SensorActive.png")}"}}}}\n'
    '  - type: pointLight2D\n    radius: 60\n    intensity: 0.5\n    flicker: 0.2\n    frequency: 0.8\n    color: "#5CD6DE"\n', oid=SENSOR)
# Gates: force fields between stone posts, see-through because neither stops the probe.
for name, oid, x, layer, tint in (("Pass A", PASS_A, 325, "    layer: 4\n", "#8FB4FF"), ("Pass B", PASS_B, 380, "    layer: 5\n    mask: 0\n", "#FFB48F")):
    obj(name, x, 155, 10, 90, sprite("Assets/Showcase/Gate.png", tint=f'"{tint}"', order=2) + "  - type: boxCollider2D\n" + layer
        + f'  - type: pointLight2D\n    radius: 44\n    intensity: 0.4\n    color: "{tint}"\n', oid=oid)
    for end, y in (("top", 141), ("foot", 243)):
        obj(f"{name} {end}", x - 3, y, 16, 16, sprite("Assets/Showcase/GatePost.png", order=3))
obj("Ramp", 500, 140, 100, 120, sprite("Assets/Showcase/RampWall.png", order=2) + "  - type: polygonCollider2D\n    layer: 2\n    mask: 2\n"
    "    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0.5}\n    - {x: 0, y: 1}\n", oid=RAMP)
obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n", oid=CAMERA)

# Names under the course (world text); HUD: the probe's readout, feature tags, caption.
obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n", oid=CANVAS)
for name, label, x in (("Probe", "Probe", 100), ("Sensor", "Sensor (trigger)", 240), ("Gates", "Gates A and B", 357), ("Ramp", "Polygon wall", 540)):
    obj(f"{name} name", x - 70, 268, 140, 18, f'  - type: text2D\n    text: "{label}"\n    size: 12\n    color: "#DCE6EE"\n    align: Center\n    sortingLayer: 5\n    order: 20\n')
readout = obj("Readout", -12, 12, 330, 64, "  - type: uiRect\n    anchor: TopRight\n" + panel(), CANVAS)
obj("Metrics", 12, 9, 306, 18, text("Ready", 13), readout, oid=METRICS)
obj("Status", 12, 34, 306, 18, text("Press → to roll the probe", 12, "#BFD0DC"), readout, oid=STATUS)
tags = [("cast", "Ray and sweep", "the line hits the wall, the circle stops 20 short"),
        ("crossed", "Sensor", "a trigger reports the probe passing through"),
        ("stopped", "Layer masks", "both gates let the probe by, the wall stops it")]
tag_w = 24 + 6.4 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', CANVAS)
caption = obj("Caption", 12, -12, 470, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), CANVAS)
obj("Caption title", 12, 5, 446, 20, text("<b>CollisionLab</b>   Rays, sweeps, sensors and layer masks", 15), caption)
obj("Caption controls", 12, 25, 446, 16, text("Roll the probe  →", 12, "#BFD0DC"), caption)

(ROOT / "Scenes/ShowcaseCasts.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {CAMERA}\n")
print("wrote Scenes/ShowcaseCasts.scene,", len(out), "objects, id", (ROOT / "Scenes/ShowcaseCasts.scene.meta").read_text().split()[1])
