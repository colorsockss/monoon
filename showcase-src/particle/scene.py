# Writes ParticleLab's showcase scene (Scenes/Showcase.scene) and its particle variants, clips and animator.
# Run after art.sh:  python3 showcase-src/particle/scene.py
#
# A night camp the hero walks through from left to right: campfire, rain cloud, fountain, firework launcher, portal.
# Each effect is a particle asset sized for this camera (zoom 2); Walker.cs walks, StationTags.cs shows the HUD tags and
# starts the 10,240-particle storm at the portal, the sample's LabControls fires the rockets on Space.
import pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/ParticleLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("5c0b0a0e-3f1d-5a55-9d8e-1f2f7c3e9a21")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
BURST = "b6aaa729-aaf8-4712-99e8-e8c64f54bb89"    # Assets/Burst.particles (firework sparks)
SPLASH = "a2b4b682-4533-4f09-bb1b-9f5ac04d7002"   # Assets/Splash.particles (rain droplets)
GROUND = 328                                      # top of the ground tiles


def png_size(name):
    w, h = struct.unpack(">II", (ART / name).read_bytes()[16:24])
    return w // 2, h // 2   # art pixels = world units


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def text_asset(name, text):
    (ART / name).write_text(text)
    return asset(name)


def clip(name, frames, seconds):
    return text_asset(name, "frames:\n" + "".join(f"- sprite: {asset(f)}\n  seconds: {seconds:g}\n" for f in frames) + "loop: true\n")


# ---- assets ---------------------------------------------------------------------------------------------------------
ground_ts = (ROOT / "../PlatformerLab/Assets/Showcase/Ground.tileset").read_text().split("\n", 1)[1]
ground_ts = text_asset("Ground.tileset", f"image: {asset('Ground.png')}\n" + ground_ts)
hero_idle = [clip(f"HeroIdle{d}.clip", [f"HeroIdle{d}.png"], 1) for d in "EW"]
hero_walk = [clip(f"HeroWalk{d}.clip", [f"HeroWalk{d}{i}.png" for i in range(1, 5)], 0.12) for d in "EW"]
four = lambda e, w: (e, e, e, w)
hero_animator = text_asset("Hero.animator", "entry: Idle\nstates:\n"
    + "".join(f"- name: {n}\n  clip: {e}\n  directions:\n" + "".join(f"  - {c}\n" for c in four(e, w)) + "  direction: Move\n" for n, (e, w) in (("Idle", hero_idle), ("Walk", hero_walk)))
    + "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\n"
    "transitions:\n- from: Idle\n  to: Walk\n  parameter: Speed\n  test: Greater\n  value: 0.1\n- from: Walk\n  to: Idle\n  parameter: Speed\n  test: AtMost\n  value: 0.1\n")

# Campfire: the sample's Flame (cone, size and colour curves, additive) at campfire size, a thin smoke and a few sparks.
camp_flame = text_asset("CampFlame.particles", """rate: 60
shape: Cone
radius: 6
spread: 26
speed: {min: 22, max: 40}
lifetime: {min: 0.5, max: 0.75}
size: {min: 5, max: 9}
color: {min: "#FFFFFF"}
gravity: -30
blend: Additive
sizeOverLifetime: {keys: [{time: 0, value: 0.6}, {time: 0.2, value: 1}, {time: 1, value: 0.2}]}
colorOverLifetime:
  colors: [{time: 0, color: "#FFF3B0"}, {time: 0.35, color: "#FF9A3C"}, {time: 1, color: "#7A1A10"}]
  alphas: [{time: 0, alpha: 0}, {time: 0.1, alpha: 1}, {time: 0.5, alpha: 0.8}, {time: 1, alpha: 0}]
""")
camp_smoke = text_asset("CampSmoke.particles", """duration: 2
prewarm: true
rate: 7
spread: 18
speed: {min: 8, max: 14}
lifetime: {min: 1.8, max: 2.4}
size: {min: 4, max: 6}
endSize: 2.6
color: {min: "#6E6878A0"}
endAlpha: 0
gravity: -6
""")
camp_sparks = text_asset("CampSparks.particles", """rate: 7
shape: Cone
radius: 4
spread: 40
speed: {min: 30, max: 60}
lifetime: {min: 0.8, max: 1.2}
size: {min: 1, max: 1.5}
color: {min: "#FFE08A", max: "#FF8A3C"}
endAlpha: 0
gravity: -10
blend: Additive
""")
# Rain: the sample's Downpour under one cloud; drops hit the ground tiles and throw Splash droplets.
cloud_rain = text_asset("CloudRain.particles", f"""maxParticles: 256
rate: 70
shape: Edge
width: 44
direction: 95
speed: {{min: 120, max: 150}}
lifetime: {{min: 1.2, max: 1.2}}
size: {{min: 1, max: 1.4}}
aspect: 4
rotation: {{min: 5, max: 5}}
color: {{min: "#9FC8FFC0"}}
endAlpha: 1
gravity: 300
collision: {{mode: Tiles, bounce: 0.3, dampen: 0.3, lifetimeLoss: 0.8}}
subEmitters: [{{when: Collision, particles: {SPLASH}, count: 2, inheritVelocity: 0.2}}]
""")
fountain = text_asset("Jet.particles", """rate: 140
shape: Cone
radius: 2
spread: 20
speed: {min: 165, max: 180}
lifetime: {min: 0.8, max: 0.85}
size: {min: 2, max: 3}
color: {min: "#8FE4FF", max: "#3F8BFF"}
endSize: 0.6
gravity: 420
""")
# Fireworks: three rockets from the launcher per press; each bursts into 48 sparks of its colour (sub-emitter on death).
rockets = text_asset("Rockets.particles", f"""duration: 1.2
looping: false
rate: 0
bursts: [{{time: 0, count: 1}}, {{time: 0.25, count: 1}}, {{time: 0.5, count: 1}}]
maxParticles: 16
shape: Edge
width: 6
direction: -90
spread: 16
speed: {{min: 150, max: 170}}
lifetime: {{min: 0.85, max: 0.95}}
size: {{min: 2, max: 2.5}}
color: {{min: "#FFD36B", max: "#6BD3FF"}}
endAlpha: 1
gravity: 180
blend: Additive
subEmitters: [{{when: Death, particles: {BURST}, count: 48, inheritVelocity: 0.15, inheritColor: true}}]
""")
# Fireflies: a few slow green-gold dots wandering on a noise field, each blinking twice over its life.
fireflies = text_asset("Fireflies.particles", """duration: 4
prewarm: true
rate: 3
maxParticles: 32
shape: Box
width: 150
height: 50
spread: 360
speed: {min: 3, max: 8}
lifetime: {min: 3.5, max: 5}
size: {min: 1.5, max: 2}
color: {min: "#E8FF8A", max: "#B8FF6A"}
drag: 0.4
noise: {strength: 18, frequency: 0.04, scroll: 0.3}
blend: Additive
colorOverLifetime: {alphas: [{time: 0, alpha: 0}, {time: 0.15, alpha: 1}, {time: 0.3, alpha: 0.15}, {time: 0.5, alpha: 1}, {time: 0.7, alpha: 0.2}, {time: 0.85, alpha: 1}, {time: 1, alpha: 0}]}
""")
storm = text_asset("PortalStorm.particles", """rate: 5120
maxParticles: 10240
shape: Circle
radius: 8
spread: 360
speed: {min: 25, max: 110}
lifetime: {min: 2, max: 2}
size: {min: 1, max: 1.5}
color: {min: "#6BE4FFB0", max: "#FF7AD9B0"}
endAlpha: 0
""")

# ---- scene ----------------------------------------------------------------------------------------------------------
out = []
def obj(name, x, y, w, h, comps, parent=None, extra=""):
    out.append(f"- id: {uid('obj/' + name)}\n" + (f"  parentId: {parent}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return uid("obj/" + name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def prop(name, image, cx, order=-2, comps="", ground=GROUND):
    """A picture standing on the ground, centred on cx."""
    w, h = png_size(image)
    return obj(name, cx - w // 2, ground - h, w, h, sprite(image, order=order) + comps)


def emitter(name, particles, x, y, parent=None, trigger="Start", order=1, extra=""):
    return obj(name, x, y, 1, 1, f"  - type: particleEmitter2D\n    particles: {particles}\n    trigger: {trigger}\n    order: {order}\n", parent,
               extra="  tag: Particles\n" + extra)


def light(radius, color, intensity=0.85, flicker=0.1):
    return f"  - type: pointLight2D\n    radius: {radius}\n    intensity: {intensity}\n    flicker: {flicker}\n    frequency: 1.2\n    color: \"{color}\"\n"


# Backdrop: sky colour fixed to the screen, moon and sky picture drifting slower than the camera.
obj("Sky backing", -400, -200, 1800, 800, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#3E2C7C"\n  - type: parallax2D\n    scrollX: 0\n    scrollY: 0\n')
obj("Moon", 280, 150, *png_size("Moon.png"), sprite("Moon.png", sortingLayer=-29) + "  - type: parallax2D\n    scrollX: 0.05\n    scrollY: 0\n")
sw, sh = png_size("Sky.png")
obj("Sky", -120, 132, sw, sh, sprite("Sky.png", sortingLayer=-30) + "  - type: parallax2D\n    scrollX: 0.15\n    scrollY: 0\n    repeatX: true\n")

# Ground: two rows of 16-unit autotile cells from x -48 to 1008.
cells = {}
for c in range(66):
    for r in range(2): cells.setdefault(c // 32, []).append(f"{{index: {r * 32 + c % 32}, tile: 1}}")
chunks = "".join(f"    - x: {k}\n      y: 0\n      cells: [{', '.join(v)}]\n" for k, v in sorted(cells.items()))
obj("Ground", -48, GROUND, 16, 16, f"  - type: tilemap2D\n    tileset: {ground_ts}\n    cellSize: {{width: 16, height: 16}}\n    order: -1\n    chunks:\n{chunks}")

# Camp: tent and lantern, then the campfire.
prop("Tent", "Tent.png", 40, order=-3)
prop("Lantern 1", "Lantern.png", 100, comps=light(56, "#FFC46B", 0.6))
fire = prop("Campfire", "Campfire.png", 190, order=-1, comps=light(80, "#FFA34C"))
emitter("Camp smoke", camp_smoke, 12, 4, fire, order=-2)
emitter("Camp flame", camp_flame, 12, 8, fire, order=2)
emitter("Camp sparks", camp_sparks, 12, 6, fire, order=3)
prop("Bench", "Bench.png", 245, order=-3)
# Rain cloud between two pines; the drops fall on the ground tiles.
prop("Pine 1", "Pine.png", 300, order=-3)
cw, ch = png_size("Cloud.png")
cloud = obj("Cloud", 360 - cw // 2, 200, cw, ch, sprite("Cloud.png", order=6))
rain = emitter("Rain", cloud_rain, cw // 2, ch - 8, cloud, order=5)
emitter("Splash", SPLASH, 0, 0, rain, trigger="Manual", order=5)
prop("Pine 2", "Pine.png", 420, order=-3)
# Fireflies over the bench and pines, and around the fountain.
emitter("Fireflies 1", fireflies, 300, GROUND - 40, order=4)
emitter("Fireflies 2", fireflies, 600, GROUND - 45, order=4)
# Fountain: water jet from the top of the basin.
fw, fh = png_size("Fountain.png")
basin = prop("Fountain", "Fountain.png", 535)
emitter("Jet", fountain, fw // 2, 3, basin, order=1)
prop("Lantern 2", "Lantern.png", 610, comps=light(56, "#FFC46B", 0.6))
# Launcher: Space (LabControls' Burst action) fires the rockets; their Burst child makes the sparks.
lw, lh = png_size("Launcher.png")
launcher = prop("Launcher", "Launcher.png", 700)
rocket_id = emitter("Rockets", rockets, lw // 2, 2, launcher, trigger="Manual", order=3, extra="  groups:\n  - Burst\n")
emitter("Burst", BURST, 0, 0, rocket_id, trigger="Manual", order=3)
# Portal: StationTags starts the 10,240-particle storm when the hero gets here.
pw, ph = png_size("Portal.png")
portal = prop("Portal", "Portal.png", 900, comps=light(110, "#B98CFF", 0.7))
storm_id = emitter("Storm", storm, pw // 2, ph // 2, portal, trigger="Manual", order=6)

# Hero: character motor body with a walking sprite child (MotionFacing picks the clip from its velocity).
hero = obj("Hero", 60, GROUND - 30, 22, 30, "  - type: boxCollider2D\n  - type: rigidbody2D\n    freezeRotation: true\n  - type: characterMotor2D\n"
           "  - type: script\n    script: Walker\n    values: {}\n"
           f"  - type: script\n    script: StationTags\n    values: {{\"StormEmitter\": \"{storm_id}\"}}\n")
hw, hh = png_size("HeroIdleE.png")
obj("Hero sprite", 0, 30 - hh, hw, hh, sprite("HeroIdleE.png", order=4) + f"  - type: spriteAnimator\n    controller: {hero_animator}\n    playOnStart: true\n"
    "  - type: script\n    script: MotionFacing\n    values: {}\n", hero)

# Camera follows the hero at 2×; it starts where following puts it (parallax layers keep their first-frame screen place).
obj("Camera", 160, 247.5, 1, 1, f"  - type: camera2D\n    zoom: 2\n    follow: {hero}\n    halfLife: 0.3\n    bounds: {{x: -40, y: 135, width: 1040, height: 225}}\n"
    "  - type: script\n    script: LabControls\n    values: {}\n")

# ---- HUD: caption and feature tags ----------------------------------------------------------------------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
caption = "Fire, rain, water and fireworks from particle assets"
width = 110 + 7.4 * len(caption)
cap = obj("Caption", 12, -12, width, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, canvas)
obj("Caption title", 12, 5, width - 24, 20, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>ParticleLab</b>   {caption}"\n    size: 15\n', cap)
obj("Caption controls", 12, 25, width - 24, 16, '  - type: uiRect\n  - type: uiText\n    text: "Walk  ← →      Fireworks  Space"\n    size: 12\n    color: "#BFD0DC"\n', cap)
tags = [("fire", "Curves + additive", "Flame colour and size over its life"), ("rain", "Tile collision", "Drops splash where they hit the ground"),
        ("water", "Cone + gravity", "A fountain jet from one emitter"), ("rockets", "Sub-emitters", "Each rocket bursts where it dies"),
        ("storm", "10,240 particles", "No allocation per frame")]
tag_w = 24 + 6.6 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', canvas)

text = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {uid('obj/Camera')}\n"
(ROOT / "Scenes/Showcase.scene").write_text(text)
meta = ROOT / "Scenes/Showcase.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/Showcase.scene with {len(out)} objects")
