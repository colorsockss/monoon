# Writes PlatformerLab's motion showcase Scenes/ShowcaseCourse.scene, one course from the sample's Joints, Paths and Slopes
# scenes (same geometry, showcase art).  Run:  python3 showcase-src/platformer/course.py
#
# The hero from the Showcase scene (side-view walk clips, no rig and no foot IK) on a Capsule Collider 2D and Character Motor 2D,
# driven by CourseRunner, crosses: a rope bridge of eight planks on Revolute Joint 2D; a pulley pair on Slider (prismatic) joints, where
# its weight sinks one deck and lifts the other; a lift on a Bezier Path Follower 2D whose curve a Line 2D draws; and a hill
# made of an Edge Collider 2D chain under a smooth Line 2D.
import math, pathlib, struct, sys, uuid
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import blocks, shutil

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab"
ART = ROOT / "Assets/Showcase"
SAMPLES = ROOT.parent
# Props from sibling samples in the same pixel style: KeyRoom's coin, SeedLab's bush and flowers.
for src, name in (("KeyRoom/Assets/Showcase/Coin.png", "Coin.png"), ("SeedLab/Assets/Showcase/Bush.png", "Bush.png"),
                  ("SeedLab/Assets/Showcase/Flowers.png", "Flowers.png")):
    shutil.copy(SAMPLES / src, ART / name)
(ART / "CoinSpark.particles").write_text("""duration: 0.4
looping: false
rate: 0
bursts: [{time: 0, count: 14}]
spread: 360
speed: {min: 30, max: 90}
lifetime: {min: 0.25, max: 0.5}
size: {min: 1.5, max: 3}
color: {min: "#FFE08C", max: "#FFFFFF"}
endSize: 0.3
blend: Additive
""")
(ART / "Confetti.particles").write_text("""duration: 0.8
looping: false
rate: 0
bursts: [{time: 0, count: 60}]
spread: 70
speed: {min: 120, max: 240}
lifetime: {min: 0.8, max: 1.4}
size: {min: 2, max: 4}
color: {min: "#FFD84A", max: "#FF6FA8"}
endSize: 0.6
gravity: 260
""")
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
# Ground, decks and planks are pictures cut from the sample's own Ground.png and Deck.png at their exact size (blocks.py).
ground = lambda w, h, **open: sprite(blocks.block(int(w), int(h), **open), order=-5) + "  - type: boxCollider2D\n"
deck = lambda w, h: sprite(blocks.squeeze("Deck.png", f"Deck{w}x{h}.png", w, h), order=1)
line = lambda pts, width, c1, c2, order=2: ("  - type: line2D\n    points:\n" + "".join("    - {" + ", ".join(f"{k}: {v:g}" for k, v in p.items()) + "}\n" for p in pts)
                                           + f'    width: {width}\n    startColor: "{c1}"\n    endColor: "{c2}"\n    order: {order}\n')

s = Scene("course")
# The Showcase scene's night sky, placed for the camera's first frame (parallax layers move from there).
s.obj("Sky backing", -400, -300, 1600, 900, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#3E2C7C"\n  - type: parallax2D\n    scrollX: 0\n    scrollY: 0\n')
s.obj("Sky", -190, -140, 912, 456, sprite("Sky.png", sortingLayer=-30) + "  - type: parallax2D\n    scrollX: 0.15\n    scrollY: 0\n    repeatX: true\n")
s.obj("Moon", 420, 30, 36, 36, sprite("Moon.png", sortingLayer=-29) + "  - type: parallax2D\n    scrollX: 0.05\n    scrollY: 0\n")
# ---- rope bridge and pulley (Joints scene geometry) -------------------------------------------------------------------
start = s.obj("Start ledge", 0, 300, 200, 150, ground(200, 150))
s.obj("Pit floor", 200, 430, 190, 20, ground(190, 20))
planks = [(200.0, 300.0, 12.6519), (223.4172, 305.2567, 9.0371), (247.1193, 309.0264, 5.4223), (271.0119, 311.2943, 1.8074),
          (295.0, 312.0513, -1.8074), (318.9881, 311.2943, -5.4223), (342.8807, 309.0264, -9.0371), (366.5828, 305.2567, -12.6519)]
prev = start
for i, (x, y, rot) in enumerate(planks):
    prev = s.obj(f"Plank {i + 1}", x, y, 24, 6, deck(24, 6) + "  - type: boxCollider2D\n  - type: rigidbody2D\n    mass: 2\n"
                 f'  - type: revoluteJoint2D\n    connected: "{prev}"\n    anchorX: -12\n    anchorY: -3\n', extra=f"  rotation: {rot}\n  groups:\n  - bridge\n")
s.obj("Far ledge", 390, 300, 80, 150, ground(80, 150) + f'  - type: revoluteJoint2D\n    connected: "{prev}"\n    anchorX: -40\n    anchorY: -75\n')
deck_b = s.oid("Pulley deck B")
for name, x, partner in (("Pulley deck A", 474, deck_b), ("Pulley deck B", 594, None)):
    s.obj(name, x, 300, 70, 12, deck(70, 12) + "  - type: boxCollider2D\n  - type: rigidbody2D\n    gravity: 0\n    freezeRotation: true\n"
          "  - type: prismaticJoint2D\n    axisX: 0\n    axisY: 1\n    limit: true\n    lower: -16\n    upper: 16\n"
          + (f'  - type: script\n    script: PulleyPair\n    values: {{"Partner": "{partner}"}}\n' if partner else ""), extra="  groups:\n  - pulley\n")
s.obj("Pulley post", 566, 140, 12, 290, sprite("BeamPost.png", order=-6))   # holds the wheel; stands in the pit between the decks
s.obj("Pulley wheel", 560, 134, 24, 24, sprite("Ball.png", order=2))
s.obj("Rope A", 0, 0, 1, 1, line([{"x": 562, "y": 146}, {"x": 509, "y": 300}], 2, "#4A3A2A", "#4A3A2A"))
s.obj("Rope B", 0, 0, 1, 1, line([{"x": 582, "y": 146}, {"x": 629, "y": 300}], 2, "#4A3A2A", "#4A3A2A"))
s.obj("Pulley pit floor", 470, 430, 220, 20, ground(220, 20))
# ---- Bezier path lift (Paths scene geometry, shifted 800 right; the start ledge joins the goal ledge) -------------------
OX = 800
s.obj("Middle ledge", 690, 300, OX + 200 - 690, 150, ground(OX + 200 - 690, 150))
s.obj("Lift pit floor", OX + 200, 430, 400, 20, ground(400, 20))
s.obj("Pillar", OX + 380, 210, 40, 220, ground(40, 220))
path = [{"x": 0, "y": 0, "outX": 0, "outY": -60}, {"x": 150, "y": -150, "inX": -80, "inY": 0, "outX": 80, "outY": 0}, {"x": 320, "y": -40, "inX": 0, "inY": -60}]
lift = s.obj("Path lift", OX + 200, 300, 80, 12, deck(80, 12) + '  - type: boxCollider2D\n  - type: rigidbody2D\n    bodyType: "Kinematic"\n'
             "  - type: pathFollower2D\n    points:\n" + "".join("    - {" + ", ".join(f"{k}: {v:g}" for k, v in p.items()) + "}\n" for p in path)
             + "    speed: 90\n    repeat: Once\n    playOnStart: false\n")
# The same curve drawn where the lift's centre travels (the follower moves the lift's top-left corner).
s.obj("Lift path", OX + 240, 306, 1, 1, line(path, 2, "#FFE08C", "#FF9F6B", order=-1))
s.obj("Lift goal ledge", OX + 600, 260, 200, 190, ground(200, 190, right=False))   # as in the Paths scene: the lift stops against it
# ---- chain hill (Edge Collider 2D) under a smooth Line 2D ---------------------------------------------------------------
HX, HW, HTOP = OX + 800, 480, 160
def hill(t):  # a cubic Bezier from (0, 100) over a crest back down to (HW, 100), in local units of a 100-high box
    p = [(0, 100), (HW * .3, 40), (HW * .7, 40), (HW, 100)]   # gentle enough for the character motor
    u = 1 - t
    return (u ** 3 * p[0][0] + 3 * u * u * t * p[1][0] + 3 * u * t * t * p[2][0] + t ** 3 * p[3][0],
            u ** 3 * p[0][1] + 3 * u * u * t * p[1][1] + 3 * u * t * t * p[2][1] + t ** 3 * p[3][1])
chain = [hill(i / 16) for i in range(17)]
s.obj("Hill", HX, HTOP, HW, 100, "  - type: edgeCollider2D\n    points:\n" + "".join(f"    - {{x: {x / HW:.5f}, y: {y / 100:.5f}}}\n" for x, y in chain))
# The hill's picture: grass along the same curve (sampled finely, so the grass line and the collider chain agree) down to the floor.
fine = [hill(i / 400) for i in range(401)]
def surface(x):
    for (x0, y0), (x1, y1) in zip(fine, fine[1:]):
        if x0 <= x <= x1: return HTOP + y0 + (y1 - y0) * (x - x0) / max(x1 - x0, 1e-6) - (HTOP + 40)
    return 60
s.obj("Hill ground", HX, HTOP + 40, HW, 450 - HTOP - 40, sprite(blocks.hill("Hill.png", HW, 450 - HTOP - 40, surface), order=-5))
s.obj("Hill floor", HX, 430, HW, 20, "  - type: boxCollider2D\n")
s.obj("End ledge", HX + HW, 260, 260, 190, ground(260, 190, left=False))
s.obj("Goal pennant", HX + HW + 200, 260 - 58, 40, 58, sprite("Pennant.png", order=1))
confetti = s.obj("Confetti", HX + HW + 220, 214, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('Confetti.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
# ---- dressing: a sign at the start, torches at both ends, bushes and flowers on the ledges (behind the runner) ---------------
s.obj("Start sign", 8, 300 - 52, 46, 52, sprite("Sign.png", order=-4))
for i, (x, top) in enumerate(((176, 300), (HX + HW + 25, 260))):
    s.obj(f"Torch post {i + 1}", x - 23, top - 32, 46, 32, sprite("TorchPost.png", order=-4))
    s.obj(f"Torch flame {i + 1}", x - 12, top - 66, 24, 36, f"  - type: spriteShader2D\n    shader: {asset('Torch.shader', 'Assets')}\n    order: -3\n")
    s.obj(f"Torch light {i + 1}", x - 1, top - 50, 2, 2, '  - type: pointLight2D\n    radius: 90\n    intensity: 0.8\n    flicker: 0.12\n    frequency: 1.4\n    color: "#FFA34C"\n')
for i, (x, top) in enumerate(((84, 300), (760, 300), (930, 300), (OX + 640, 260), (HX + HW + 60, 260))):
    s.obj(f"Bush {i + 1}", x, top - 38, 50, 42, sprite("Bush.png", order=-4))
for i, (x, top) in enumerate(((40, 300), (840, 300), (OX + 720, 260), (HX + HW + 105, 260))):
    s.obj(f"Flowers {i + 1}", x, top - 28, 54, 32, sprite("Flowers.png", order=-4))
# ---- coins along the way, at the runner's middle (his feet on the surface, 28 below his centre) ---------------------------
hill_top = lambda x: HTOP + 40 + surface(x - HX)
coins = [(130, 266), (165, 266), (250, 280), (295, 286), (340, 280), (760, 266), (820, 266), (880, 266), (940, 266),
         (OX + 650, 226), (OX + 710, 226), (OX + 770, 226)] + [(x, hill_top(x) - 30) for x in range(HX + 60, HX + HW - 40, 60)] \
        + [(HX + HW + 40, 226), (HX + HW + 90, 226)]
coin_ids = [s.obj(f"Coin {i + 1}", x - 7, y - 7, 14, 14, sprite("Coin.png", order=5)) for i, (x, y) in enumerate(coins)]
spark = s.obj("Coin spark", 0, 0, 1, 1, f"  - type: particleEmitter2D\n    particles: {asset('CoinSpark.particles')}\n    trigger: Manual\n    sortingLayer: 5\n")
# ---- runner --------------------------------------------------------------------------------------------------------
# The body is a capsule; the picture is a child with the Showcase hero's animator, turned and stepped by MotionFacing.
RW, RH = 24, 56
runner = s.obj("Runner", 60, 300 - RH, RW, RH, "  - type: capsuleCollider2D\n    layer: 1\n  - type: rigidbody2D\n    freezeRotation: true\n"
               f"  - type: particleEmitter2D\n    particles: {asset('LandingDust.particles')}\n    trigger: Landing\n    offsetY: {RH // 2}\n  - type: characterMotor2D\n"
               f'  - type: script\n    script: Showcase/CourseRunner\n    values: {{"Lift": "{lift}", "DeckA": "{s.oid("Pulley deck A")}", "DeckB": "{deck_b}", '
               f'"RopeA": "{s.oid("Rope A")}", "RopeB": "{s.oid("Rope B")}", "Spark": "{spark}", "Confetti": "{confetti}", "Counter": "{s.oid("Coin count")}", '
               '"Coins": [' + ", ".join(f'"{c}"' for c in coin_ids) + "]}\n")
s.obj("Hero", (RW - 42) / 2, RH - 60, 42, 60, sprite("HeroIdleE.png", order=4) + f"  - type: spriteAnimator\n    controller: {asset('Hero.animator')}\n    playOnStart: true\n"
      "  - type: script\n    script: MotionFacing\n    values: {}\n", parent="Runner")
s.obj("Camera", 400, 225, 1, 1, f"  - type: camera2D\n    zoom: 1.5\n    follow: {runner}\n    halfLife: 0.3\n    bounds: {{x: 0, y: 40, width: {HX + HW + 260}, height: 410}}\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
tags = [("motor", "Character Motor 2D", "a capsule hero walks it all"), ("bridge", "Revolute Joint 2D", "a rope bridge of planks"),
        ("pulley", "Slider joints", "a pulley: one deck sinks, one rises"), ("path", "Path Follower 2D", "a lift on a Bezier curve"),
        ("chain", "Edge Collider 2D", "a curved hill on a collider chain")]   # in the order they light up
tw = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 28, tw, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
s.obj("Coin panel", -12, 12, 150, 56, "  - type: uiRect\n    anchor: TopRight\n" + panel(), parent="Canvas")
s.obj("Coin label", 12, 8, 126, 14, '  - type: uiRect\n  - type: uiText\n    text: "COINS"\n    size: 11\n    color: "#BFD0DC"\n', parent="Coin panel")
s.obj("Coin count", 12, 22, 126, 30, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "0 / {len(coins)}"\n    size: 24\n    color: "#FFD84A"\n', parent="Coin panel")
s.obj("Caption", 12, -12, 400, 30, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 5, 380, 20, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>PlatformerLab</b>   Joints, curves and a moving lift"\n    size: 15\n', parent="Caption")
(ROOT / "Scenes/ShowcaseCourse.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseCourse.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseCourse.scene')}\n")
print("scene", meta.read_text().split()[1])
