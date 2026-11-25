# Writes PlatformerLab's motion showcase Scenes/ShowcaseCourse.scene, one course from the sample's Joints, Paths and Slopes
# scenes (same geometry, showcase art).  Run:  python3 showcase-src/platformer/course.py
#
# A skeleton-animated capsule runner (Skeleton Animator 2D on Travel clock, Capsule Collider 2D, Character Motor 2D) driven by
# CourseRunner crosses: a rope bridge of eight planks on Revolute Joint 2D; a pulley pair on Slider (prismatic) joints, where
# its weight sinks one deck and lifts the other; a lift on a Bezier Path Follower 2D whose curve a Line 2D draws; and a hill
# made of an Edge Collider 2D chain under a smooth Line 2D.
import math, pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("3b309c28-75ce-562e-92ac-f94ee48b18c7")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def asset(name, folder="Assets/Showcase"):
    meta = ROOT / folder / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None, extra=""):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, folder="Assets/Showcase", **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image, folder)}\n    asset: {folder}/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('UiShade.png', 'Assets/Ui')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
ground = lambda order=-5: sprite("Ground.png", drawMode="Tiled", order=order) + "  - type: boxCollider2D\n"
line = lambda pts, width, c1, c2, order=2: ("  - type: line2D\n    points:\n" + "".join("    - {" + ", ".join(f"{k}: {v:g}" for k, v in p.items()) + "}\n" for p in pts)
                                           + f'    width: {width}\n    startColor: "{c1}"\n    endColor: "{c2}"\n    order: {order}\n')

s = Scene("course")
s.obj("Sky", -200, -200, 2800, 420, '  - type: spriteRenderer\n    squareColor: "#8FC9E8"\n    order: -40\n')
s.obj("Sky low", -200, 220, 2800, 500, '  - type: spriteRenderer\n    squareColor: "#BFE3EE"\n    order: -40\n')
# ---- rope bridge and pulley (Joints scene geometry) -------------------------------------------------------------------
start = s.obj("Start ledge", 0, 300, 200, 150, ground())
s.obj("Pit floor", 200, 430, 190, 20, ground())
planks = [(200.0, 300.0, 12.6519), (223.4172, 305.2567, 9.0371), (247.1193, 309.0264, 5.4223), (271.0119, 311.2943, 1.8074),
          (295.0, 312.0513, -1.8074), (318.9881, 311.2943, -5.4223), (342.8807, 309.0264, -9.0371), (366.5828, 305.2567, -12.6519)]
prev = start
for i, (x, y, rot) in enumerate(planks):
    prev = s.obj(f"Plank {i + 1}", x, y, 24, 6, sprite("Deck.png", drawMode="Tiled", order=1) + "  - type: boxCollider2D\n  - type: rigidbody2D\n    mass: 2\n"
                 f'  - type: revoluteJoint2D\n    connected: "{prev}"\n    anchorX: -12\n    anchorY: -3\n', extra=f"  rotation: {rot}\n  groups:\n  - bridge\n")
s.obj("Far ledge", 390, 300, 80, 150, ground() + f'  - type: revoluteJoint2D\n    connected: "{prev}"\n    anchorX: -40\n    anchorY: -75\n')
deck_b = s.oid("Pulley deck B")
for name, x, partner in (("Pulley deck A", 474, deck_b), ("Pulley deck B", 594, None)):
    s.obj(name, x, 300, 70, 12, sprite("Deck.png", drawMode="Tiled", order=1) + "  - type: boxCollider2D\n  - type: rigidbody2D\n    gravity: 0\n    freezeRotation: true\n"
          "  - type: prismaticJoint2D\n    axisX: 0\n    axisY: 1\n    limit: true\n    lower: -16\n    upper: 16\n"
          + (f'  - type: script\n    script: PulleyPair\n    values: {{"Partner": "{partner}"}}\n' if partner else ""), extra="  groups:\n  - pulley\n")
s.obj("Pulley wheel", 560, 134, 24, 24, sprite("Ball.png", order=2))
s.obj("Rope A", 0, 0, 1, 1, line([{"x": 562, "y": 146}, {"x": 509, "y": 300}], 2, "#4A3A2A", "#4A3A2A"))
s.obj("Rope B", 0, 0, 1, 1, line([{"x": 582, "y": 146}, {"x": 629, "y": 300}], 2, "#4A3A2A", "#4A3A2A"))
s.obj("Pulley pit floor", 470, 430, 220, 20, ground())
# ---- Bezier path lift (Paths scene geometry, shifted 800 right; the start ledge joins the goal ledge) -------------------
OX = 800
s.obj("Middle ledge", 690, 300, OX + 200 - 690, 150, ground())
s.obj("Lift pit floor", OX + 200, 430, 400, 20, ground())
s.obj("Pillar", OX + 380, 210, 40, 220, sprite("Pillar.png", order=-6) + "  - type: boxCollider2D\n")
path = [{"x": 0, "y": 0, "outX": 0, "outY": -60}, {"x": 150, "y": -150, "inX": -80, "inY": 0, "outX": 80, "outY": 0}, {"x": 320, "y": -40, "inX": 0, "inY": -60}]
lift = s.obj("Path lift", OX + 200, 300, 80, 12, sprite("Deck.png", drawMode="Tiled", order=1) + '  - type: boxCollider2D\n  - type: rigidbody2D\n    bodyType: "Kinematic"\n'
             "  - type: pathFollower2D\n    points:\n" + "".join("    - {" + ", ".join(f"{k}: {v:g}" for k, v in p.items()) + "}\n" for p in path)
             + "    speed: 90\n    repeat: Once\n    playOnStart: false\n")
# The same curve drawn where the lift's centre travels (the follower moves the lift's top-left corner).
s.obj("Lift path", OX + 240, 306, 1, 1, line(path, 2, "#FFE08C", "#FF9F6B", order=-1))
s.obj("Lift goal ledge", OX + 600, 260, 200, 190, ground())   # as in the Paths scene: the lift stops against it
# ---- chain hill (Edge Collider 2D) under a smooth Line 2D ---------------------------------------------------------------
HX, HW, HTOP = OX + 800, 480, 160
def hill(t):  # a cubic Bezier from (0, 100) over a crest back down to (HW, 100), in local units of a 100-high box
    p = [(0, 100), (HW * .3, 40), (HW * .7, 40), (HW, 100)]   # gentle enough for the character motor
    u = 1 - t
    return (u ** 3 * p[0][0] + 3 * u * u * t * p[1][0] + 3 * u * t * t * p[2][0] + t ** 3 * p[3][0],
            u ** 3 * p[0][1] + 3 * u * u * t * p[1][1] + 3 * u * t * t * p[2][1] + t ** 3 * p[3][1])
chain = [hill(i / 16) for i in range(17)]
s.obj("Hill", HX, HTOP, HW, 100, "  - type: edgeCollider2D\n    points:\n" + "".join(f"    - {{x: {x / HW:.5f}, y: {y / 100:.5f}}}\n" for x, y in chain)
      + line([{"x": 0, "y": 100, "outX": HW * .3, "outY": -60}, {"x": HW, "y": 100, "inX": -HW * .3, "inY": -60}], 6, "#7CC36A", "#4F9A55"))
s.obj("Hill floor", HX, 430, HW, 20, ground())
s.obj("End ledge", HX + HW, 260, 260, 190, ground())
s.obj("Goal pennant", HX + HW + 200, 196, 40, 64, sprite("Pennant.png", order=1))
# ---- runner --------------------------------------------------------------------------------------------------------
runner = s.obj("Runner", 60, 268, 22, 30, f"  - type: skeletonAnimator2D\n    rig: {asset('Runner.rig', 'Assets')}\n    order: 3\n    clock: Travel\n    stride: 28\n"
               "  - type: capsuleCollider2D\n    layer: 1\n  - type: rigidbody2D\n    freezeRotation: true\n  - type: characterMotor2D\n"
               f'  - type: script\n    script: Showcase/CourseRunner\n    values: {{"Lift": "{lift}", "DeckA": "{s.oid("Pulley deck A")}", "DeckB": "{deck_b}", '
               f'"RopeA": "{s.oid("Rope A")}", "RopeB": "{s.oid("Rope B")}"}}\n')
s.obj("Camera", 400, 225, 1, 1, f"  - type: camera2D\n    zoom: 1.5\n    follow: {runner}\n    halfLife: 0.3\n    bounds: {{x: 0, y: 40, width: {HX + HW + 260}, height: 410}}\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
tags = [("rig", "Skeleton Animator 2D", "bones on a capsule body"), ("bridge", "Revolute Joint 2D", "a rope bridge of planks"),
        ("pulley", "Slider joints", "a pulley: one deck sinks, one rises"), ("path", "Path Follower 2D", "a lift on a Bezier curve"),
        ("chain", "Edge Collider 2D", "a chain hill under a smooth Line 2D")]   # in the order they light up
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 26, tw, 22, f"  - type: uiRect\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 3\n', parent="Canvas")
s.obj("Caption", 12, -8, 330, 24, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 3, 310, 18, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>PlatformerLab</b>   Joints, curves and a rigged runner"\n    size: 13\n', parent="Caption")
(ROOT / "Scenes/ShowcaseCourse.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseCourse.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseCourse.scene')}\n")
print("scene", meta.read_text().split()[1])
