# Writes CollisionLab's second showcase, Scenes/ShowcaseCasts.scene: the collision lab's probe course in the lantern scene's art.
#   python3 showcase-src/collision/casts.py     (after art.py)
#
# The objects keep the lab's ids, names, positions and colliders, so the engine checks that measure them (CollisionChecks,
# CollisionLab.InspectorEdits) see the same geometry: a probe circle (r 20) at (80, 180), a sensor ring (trigger), two gates on
# layers outside the probe's reach (A is not in the probe's mask; B's own mask is empty) and a solid triangle on the wall layer.
# Probe casts a ray and sweeps its circle toward the triangle on the first step and draws both (the line to the hit, a ghost
# where the circle stops); → moves the probe with one 800-unit Move, through the sensor and both gates, against the triangle.
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

obj("Night grass", 0, 0, 800, 450, sprite("Assets/Showcase/NightGrass.png", drawMode="Tiled", order=-20))
ray = obj("Ray", 0, 0, 1, 1, '  - type: line2D\n    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0}\n    width: 2\n    startColor: "#FFE08C"\n    endColor: "#FF9F6B"\n    order: 4\n')
ghost = obj("Sweep stop", 0, 0, 40, 40, sprite("Assets/Showcase/PebbleLit.png", tint='"#9C98B8"', order=3), extra="  active: false\n")
obj("Probe", 80, 180, 40, 40, sprite("Assets/Showcase/PebbleLit.png", order=10) + "  - type: circleCollider2D\n    layer: 1\n    mask: 44\n"
    "  - type: script\n    script: Probe\n    values: {" + f'"Status": "{STATUS}", "Metrics": "{METRICS}", "Wall": "{RAMP}", "Sensor": "{SENSOR}", "RayLine": "{ray}", "SweepStop": "{ghost}"' + "}\n", oid=PROBE)
obj("Sensor", 218, 178, 44, 44, sprite("Assets/Showcase/Lantern.png", tint='"#9FE4FF"', order=2) + "  - type: circleCollider2D\n    layer: 3\n    mask: 2\n    isTrigger: true\n"
    "  - type: script\n    script: SensorEntry\n", oid=SENSOR)
# Gates: posts of stone blocks, see-through because neither stops the probe.
obj("Pass A", 325, 155, 10, 90, sprite("Assets/Showcase/Block.png", drawMode="Tiled", tint='"#8FB4FF"', order=2) + "  - type: boxCollider2D\n    layer: 4\n", oid=PASS_A)
obj("Pass B", 380, 155, 10, 90, sprite("Assets/Showcase/Block.png", drawMode="Tiled", tint='"#FFB48F"', order=2) + "  - type: boxCollider2D\n    layer: 5\n    mask: 0\n", oid=PASS_B)
obj("Ramp", 500, 140, 100, 120, sprite("Assets/Triangle.png", tint='"#B7A6E8"', order=2) + "  - type: polygonCollider2D\n    layer: 2\n    mask: 2\n"
    "    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0.5}\n    - {x: 0, y: 1}\n", oid=RAMP)
obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n", oid=CAMERA)

# Names under the course (world text); HUD: the probe's readout, feature tags, caption.
obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n", oid=CANVAS)
for name, label, x in (("Probe", "Probe", 100), ("Sensor", "Sensor (trigger)", 240), ("Gates", "Gates A and B", 357), ("Ramp", "Polygon wall", 540)):
    obj(f"{name} name", x - 70, 268, 140, 18, f'  - type: text2D\n    text: "{label}"\n    size: 12\n    color: "#BFD0DC"\n    align: Center\n    order: 20\n')
readout = obj("Readout", -12, 12, 330, 64, "  - type: uiRect\n    anchor: TopRight\n" + panel(), CANVAS)
obj("Metrics", 12, 9, 306, 18, text("Ready", 13), readout, oid=METRICS)
obj("Status", 12, 34, 306, 18, text("Press → to move the probe", 12, "#BFD0DC"), readout, oid=STATUS)
tags = [("cast", "Ray and sweep", "the line hits the wall, the circle stops 20 short"),
        ("crossed", "Sensor", "a trigger reports the probe passing through"),
        ("stopped", "Layer masks", "both gates let the probe by, the wall stops it")]
tag_w = 24 + 6.4 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', CANVAS)
caption = obj("Caption", 12, -12, 470, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), CANVAS)
obj("Caption title", 12, 5, 446, 20, text("<b>CollisionLab</b>   Rays, sweeps, sensors and layer masks", 15), caption)
obj("Caption controls", 12, 25, 446, 16, text("Move the probe  →", 12, "#BFD0DC"), caption)

(ROOT / "Scenes/ShowcaseCasts.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {CAMERA}\n")
print("wrote Scenes/ShowcaseCasts.scene,", len(out), "objects, id", (ROOT / "Scenes/ShowcaseCasts.scene.meta").read_text().split()[1])
