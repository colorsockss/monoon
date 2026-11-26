# Writes CollisionLab's second showcase, Scenes/ShowcaseCasts.scene: the collision lab's probe course in a torch-lit corridor.
#   python3 showcase-src/collision/casts.py     (after art.py)
#
# Art: KeyRoom's flagstones, brick wall and torches (Point Light 2D, the left one casts shadows of the gates and the wall)
# under a Global Light 2D; the probe is a steel sphere with its own small light, the sensor a rune plate that lights up
# (SensorEntry swaps in SensorActive), the gates tinted force fields between stone posts, the wall KeyRoom's bricks cut to
# the triangle. Names and the ray draw on the unlit layer. Decoration has no colliders, so the measured geometry is unchanged.
#
# The objects keep the lab's ids, names and colliders, so the engine checks that measure them (CollisionChecks,
# CollisionLab.InspectorEdits) see the same geometry: a probe circle (r 20) at (80, 180), a sensor ring (trigger), two gates on
# layers outside the probe's reach (A is not in the probe's mask; B's own mask is empty) and a solid triangle on the wall layer.
# The video has to say what the gates are for, so every object carries a label (name, layer, the rule the probe meets there)
# and the casts are colour-coded: the probe's ray and sweep (mask Solid) reach the wall, a blue ray whose mask adds Ghost stops
# at gate A and a pink one whose mask adds Glass stops at gate B. → rolls the probe at 120 units per second through the sensor
# and both gates, which flicker (ProbeTouch) and say "probe passes", against the wall, which flashes and says it stops it.
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
for i, (x, f) in enumerate(((400, 1.4), (590, 1.1), (760, 1.25))):  # right of the legend
    torch = obj(f"Torch {i + 1}", x, 110, 32, 22, sprite("Assets/Showcase/TorchSconce.png", sortingLayer=5)
                + TORCH.format(f=f, shadows="    shadows: true\n" if i == 0 else ""))
    obj(f"Torch {i + 1} flame", 0, -32, 32, 32, sprite("Assets/Showcase/TorchFlame1.png", sortingLayer=5, order=1)
        + f"  - type: spriteAnimator\n    clip: {asset('Assets/Showcase/Flame.clip')}\n    playOnStart: true\n", parent=torch)
# Layer names are this showcase's labels for the numbers the colliders use: 1 Probe, 2 Solid, 3 Sensor, 4 Ghost, 5 Glass.
SOLID, GHOST, GLASS, PASSES, STOPS = "#FFE08C", "#8FB4FF", "#F49AE8", "#9CF09C", "#FF9A8A"
line = lambda name, color: obj(name, 0, 0, 1, 1, f'  - type: line2D\n    points:\n    - {{x: 0, y: 0}}\n    - {{x: 1, y: 0}}\n    width: 2\n'
                               f'    startColor: "{color}"\n    endColor: "{color}"\n    sortingLayer: 5\n    order: 4\n')
ray, ghost_ray, glass_ray = line("Ray", SOLID), line("Ray ghost", GHOST), line("Ray glass", GLASS)
ghost = obj("Sweep stop", 0, 0, 40, 40, sprite("Assets/Showcase/Probe.png", tint='"#9A98C8"', order=3), extra="  active: false\n")
obj("Probe", 80, 180, 40, 40, sprite("Assets/Showcase/Probe.png", order=10) + "  - type: circleCollider2D\n    layer: 1\n    mask: 44\n"
    "  - type: script\n    script: Probe\n    values: {" + f'"Status": "{STATUS}", "Metrics": "{METRICS}", "Wall": "{RAMP}", "Sensor": "{SENSOR}", '
    f'"RayLine": "{ray}", "SweepStop": "{ghost}", "GhostRay": "{ghost_ray}", "GlassRay": "{glass_ray}"' + "}\n"
    '  - type: pointLight2D\n    radius: 56\n    intensity: 0.45\n    color: "#FFD087"\n', oid=PROBE)
obj("Probe shadow", 4, 34, 32, 10, sprite("Assets/Showcase/ShadowM.png", order=-1), parent=PROBE)


# World labels under the course: name and layer, what the probe meets there, and a note the object fills in when it happens.
def label(name, x, y, title, rule, leader_from=None):
    t = lambda suffix, dy, text, size, color: obj(f"{name} {suffix}", x - 90, y + dy, 180, 16,
        f'  - type: text2D\n    text: "{text}"\n    size: {size}\n    color: "{color}"\n    align: Center\n    richText: true\n    sortingLayer: 5\n    order: 20\n')
    t("name", 0, title, 14, "#F4F6F4"); t("rule", 16, rule, 12, "#BFD0DC")
    if leader_from:  # second-row labels hang from their object by a thin line
        obj(f"{name} leader", 0, 0, 1, 1, f'  - type: line2D\n    points:\n    - {{x: {x}, y: {leader_from}}}\n    - {{x: {x}, y: {y - 2}}}\n'
            '    width: 1\n    startColor: "#8A93B8"\n    endColor: "#8A93B8"\n    sortingLayer: 5\n    order: 19\n')
    return t("note", 32, "", 13, "#F4F6F4")


sensor_note = label("Sensor", 240, 318, "<b>Sensor</b>  trigger, layer Sensor", "reports what passes through", 224)
obj("Sensor", 218, 178, 44, 44, sprite("Assets/Showcase/SensorIdle.png", order=2) + "  - type: circleCollider2D\n    layer: 3\n    mask: 2\n    isTrigger: true\n"
    f'  - type: script\n    script: SensorEntry\n    values: {{"Active": {{"Id": "{asset("Assets/Showcase/SensorActive.png")}"}}, "Note": "{sensor_note}"}}\n'
    '  - type: pointLight2D\n    radius: 60\n    intensity: 0.5\n    flicker: 0.2\n    frequency: 0.8\n    color: "#5CD6DE"\n', oid=SENSOR)
touch = lambda note, says, flash: (f'  - type: script\n    script: Showcase/ProbeTouch\n    values: {{"Probe": "{PROBE}", "Note": "{note}", '
                                   f'"Says": "{says}", "Flash": "{flash}"}}\n')
passes = f"<color={PASSES}><b>probe passes</b></color>"
# Gates: force fields between stone posts, see-through because neither stops the probe; each flickers as the probe goes through.
for name, oid, x, layer, tint, row, title, rule in (
        ("Pass A", PASS_A, 305, "    layer: 4\n", GHOST, (262, None), f"<b>Gate A</b>  layer <color={GHOST}>Ghost</color>", "not in the probe's mask"),
        ("Pass B", PASS_B, 405, "    layer: 5\n    mask: 0\n", GLASS, (318, 260), f"<b>Gate B</b>  layer <color={GLASS}>Glass</color>", "its own mask is empty")):
    note = label(name, x + 5, row[0], title, rule, row[1])
    obj(name, x, 155, 10, 90, sprite("Assets/Showcase/Gate.png", tint=f'"{tint}"', order=2) + "  - type: boxCollider2D\n" + layer
        + f'  - type: pointLight2D\n    radius: 44\n    intensity: 0.4\n    color: "{tint}"\n' + touch(note, passes, "#FFFFFF"), oid=oid)
    for end, y in (("top", 141), ("foot", 243)):
        obj(f"{name} {end}", x - 3, y, 16, 16, sprite("Assets/Showcase/GatePost.png", order=3))
wall_note = label("Ramp", 550, 262, f"<b>Wall</b>  layer <color={SOLID}>Solid</color>", "masks match both ways")
obj("Ramp", 500, 140, 100, 120, sprite("Assets/Showcase/RampWall.png", order=2) + "  - type: polygonCollider2D\n    layer: 2\n    mask: 2\n"
    "    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0.5}\n    - {x: 0, y: 1}\n" + touch(wall_note, f"<color={STOPS}><b>stops the probe</b></color>", STOPS), oid=RAMP)
label("Probe", 100, 262, "<b>Probe</b>  rolls with →", f"mask: <color={SOLID}>Solid</color>, Sensor, <color={GLASS}>Glass</color>")
obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n", oid=CAMERA)

# HUD: what each query asked for (shown once the probe has cast), the probe's readout, caption.
obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n", oid=CANVAS)
legend = [f"<b>Queries pick the layers they stop at</b>",
          f"<color={SOLID}><b>Ray</b>  mask Solid</color>   passes both gates, hits the wall",
          f"<color={GHOST}><b>Ray</b>  mask Ghost + Solid</color>   stops at Gate A",
          f"<color={GLASS}><b>Ray</b>  mask Glass + Solid</color>   stops at Gate B",
          f"<color=#B8B6E8><b>Sweep</b>  mask Solid</color>   the probe's circle stops 20 short"]
key = obj("Legend", 12, 12, 360, 20 + 18 * len(legend), "  - type: uiRect\n    anchor: TopLeft\n    showIf: cast\n" + panel(), CANVAS)
for i, t in enumerate(legend):
    obj(f"Legend {i + 1}", 10, 9 + 18 * i, 340, 18, text(t, 12), key)
readout = obj("Readout", -12, 12, 330, 64, "  - type: uiRect\n    anchor: TopRight\n" + panel(), CANVAS)
obj("Metrics", 12, 9, 306, 18, text("Ready", 13), readout, oid=METRICS)
obj("Status", 12, 34, 306, 18, text("Press → to roll the probe", 12, "#BFD0DC"), readout, oid=STATUS)
caption = obj("Caption", 12, -12, 560, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), CANVAS)
obj("Caption title", 12, 5, 536, 20, text("<b>CollisionLab</b>   Layer masks decide what rays, sweeps and colliders stop at", 15), caption)
obj("Caption controls", 12, 25, 536, 16, text("Raycast, Sweep, trigger sensor, PhysicsFilter     Roll the probe  →", 12, "#BFD0DC"), caption)

(ROOT / "Scenes/ShowcaseCasts.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {CAMERA}\n")
print("wrote Scenes/ShowcaseCasts.scene,", len(out), "objects, id", (ROOT / "Scenes/ShowcaseCasts.scene.meta").read_text().split()[1])
