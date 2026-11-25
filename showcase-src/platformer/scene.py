# Writes PlatformerLab's showcase scene (Scenes/Showcase.scene) and its tilesets, clips, animators and metas.
# Run after art.sh:  python3 showcase-src/platformer/scene.py
#
# The game objects that carry logic (runner, incline, ledge, deck, shuttle, pendulum, lookout, cutscene camera, walk
# region) are copied from the retired Scenes/Platform.scene (platform-objects.yaml) with every object id remapped, so Runner.cs and Lookout.cs run unchanged.
# Their coloured squares are replaced by the pixel art in Assets/Showcase; new scripts: BeatFlags, MotionFacing, ChainLinks.
import pathlib, re, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/PlatformerLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("3b309c28-75ce-562e-92ac-f94ee48b18c7")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
GUID = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}")
PANEL = "bf477ce7-700b-4462-ba37-ecad01ce5ee2"   # the sample's HUD frame (Assets/Ui), 9-sliced


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
ridge = (ROOT / "Assets/Ridge.tileset").read_text()
ground_ts = text_asset("Ground.tileset", re.sub(r"^image: \S+", f"image: {asset('Ground.png')}", ridge, flags=re.M))
incline = (ROOT / "Assets/Incline.tileset").read_text()   # five 38×90 cells; keep their collision polygons
incline = re.sub(r"^image: \S+", f"image: {asset('Incline.png')}", incline, flags=re.M)
incline = incline.replace("tileSize: {width: 152, height: 360}", "tileSize: {width: 76, height: 180}")
incline = re.sub(r"source: \{x: (\d+), y: 0, width: 152, height: 360\}", lambda m: f"source: {{x: {int(m[1]) // 2}, y: 0, width: 76, height: 180}}", incline)
incline_ts = text_asset("Incline.tileset", incline)
ledge_ts = text_asset("Ledge.tileset", f"image: {asset('Ledge.png')}\ntileSize: {{width: 32, height: 12}}\nmargin: 0\nspacing: 0\nnextId: 2\ntiles:\n"
                      "- id: 1\n  source: {x: 0, y: 0, width: 32, height: 12}\n  solid: true\n  collision:\n    oneWay: true\n    vertices: [{x: 0, y: 0}, {x: 1, y: 0}]\n")
flame = clip("Flame.clip", ["TorchFlame1.png", "TorchFlame2.png", "TorchFlame1.png", "TorchFlame3.png"], 0.16)
walk_e = clip("GuardWalkE.clip", [f"Guard{i}.png" for i in range(1, 5)], 0.16)
walk_w = clip("GuardWalkW.clip", [f"GuardW{i}.png" for i in range(1, 5)], 0.16)
guard_animator = text_asset("Guard.animator", "entry: Walk\nstates:\n- name: Walk\n  clip: " + walk_e + "\n  directions:\n"
                            + "".join(f"  - {c}\n" for c in (walk_e, walk_e, walk_e, walk_w)) + "  direction: Move\n"
                            "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\ntransitions: []\n")
# Hero: side-view walk sprites in a 4-direction set (N and S reuse E); MotionFacing feeds it the runner's velocity.
hero_idle = [clip(f"HeroIdle{d}.clip", [f"HeroIdle{d}.png"], 1) for d in "EW"]
hero_walk = [clip(f"HeroWalk{d}.clip", [f"HeroWalk{d}{i}.png" for i in range(1, 5)], 0.12) for d in "EW"]
four = lambda e, w: (e, e, e, w)
hero_animator = text_asset("Hero.animator", "entry: Idle\nstates:\n"
    + "".join(f"- name: {n}\n  clip: {e}\n  directions:\n" + "".join(f"  - {c}\n" for c in four(e, w)) + "  direction: Move\n" for n, (e, w) in (("Idle", hero_idle), ("Walk", hero_walk)))
    + "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\n"
    "transitions:\n- from: Idle\n  to: Walk\n  parameter: Speed\n  test: Greater\n  value: 0.1\n- from: Walk\n  to: Idle\n  parameter: Speed\n  test: AtMost\n  value: 0.1\n")

# Landing dust: low grey-brown puffs that spread sideways along the ground, drift up a little, grow and fade.
# A 6-art-pixel puff texture (two tones, hard edges) instead of the built-in soft dot, to match the pixel art.
def puff_png(name, n=6):
    light, mid, clear = (214, 198, 184, 255), (160, 142, 150, 255), (0, 0, 0, 0)
    rows = []
    for y in range(n):
        row = []
        for x in range(n):
            dx, dy = x + .5 - n / 2, y + .5 - n / 2
            row.append(clear if dx * dx + dy * dy > (n / 2) ** 2 else light if dx + dy < 0 else mid)
        rows += [[p for p in row for _ in (0, 1)]] * 2
    import zlib
    raw = b"".join(b"\x00" + bytes(c for p in r for c in p) for r in rows)
    chunk = lambda k, d: struct.pack(">I", len(d)) + k + d + struct.pack(">I", zlib.crc32(k + d))
    (ART / name).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", n * 2, n * 2, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
puff_png("DustPuff.png")
landing_dust = text_asset("LandingDust.particles", f"""duration: 0.6
looping: false
maxParticles: 64
rate: 0
bursts: [{{time: 0, count: 16}}]
shape: Edge
width: 14
direction: -90
spread: 170
lifetime: {{min: 0.35, max: 0.6}}
speed: {{min: 18, max: 52}}
size: {{min: 4, max: 7}}
color: {{min: "#B8A89C", max: "#8C7B84"}}
endSize: 1.8
endAlpha: 0
gravity: -12
space: World
texture: {asset("DustPuff.png")}
blend: Alpha
""")

# ---- copy the logic objects from Platform.scene -------------------------------------------------------------------
source = (pathlib.Path(__file__).resolve().parent / "platform-objects.yaml").read_text()   # the retired Platform scene
blocks = {}
for block in re.split(r"\n(?=- id: )", source.split("objects:\n", 1)[1]):
    name = re.search(r"\n  name: (.*)", block)[1]
    blocks.setdefault(name, block.rstrip("\n") + "\n")
object_ids = set(re.findall(r"^- id: (\S+)", source, re.M))
remap = lambda text: GUID.sub(lambda m: uid("obj/" + m[0]) if m[0] in object_ids else m[0], text)
old_id = lambda name: re.match(r"- id: (\S+)", blocks[name])[1]
new_id = lambda name: uid("obj/" + old_id(name))


def drop_component(block, kind):
    """Remove one component (its '  - type:' line and its deeper lines)."""
    return re.sub(rf"  - type: {kind}\n(?:    .*\n)*", "", block, count=1)


out = []
def keep(name, edit=lambda b: b):
    out.append(remap(edit(blocks[name])))


def obj(name, x, y, w, h, comps, parent=None, extra=""):
    out.append(f"- id: {uid('new/' + name)}\n" + (f"  parentId: {parent}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return uid("new/" + name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def picture(name, image, x, y, parent, **kw):
    w, h = png_size(image)
    return obj(name, x, y, w, h, sprite(image, **kw), parent)


# Runner: no skeleton or foot IK (user decision 2026-11-25); a walking sprite child, and BeatFlags for the HUD tags.
def runner(b):
    b = drop_component(b, "skeletonAnimator2D")
    b = b.replace("particles: 1cf31b57-f933-e958-b00f-7ff94fcfaae8", f"particles: {landing_dust}").replace("    offsetY: 13\n", "    offsetY: 14\n")
    return b + (f"  - type: script\n    script: BeatFlags\n    values: {{\"Ledge\": \"{old_id('Jump-through ledge')}\", \"Ramp\": \"{old_id('Incline')}\", "
                f"\"Ferry\": \"{old_id('Shuttle')}\", \"Lookout\": \"{old_id('Lookout')}\"}}\n")
keep("Runner", runner)
hw, hh = png_size("HeroIdleE.png")
obj("Hero", 0, 30 - hh, hw, hh, sprite("HeroIdleE.png", order=4) + f"  - type: spriteAnimator\n    controller: {hero_animator}\n    playOnStart: true\n"
    "  - type: script\n    script: MotionFacing\n    values: {}\n", new_id("Runner"))

keep("Incline", lambda b: b.replace("f8fa9ccd-cd95-4406-925a-c1b89092c2e8", incline_ts))
keep("Jump-through ledge", lambda b: b.replace("04a6e926-3487-575d-a5e8-83908efd75c5", ledge_ts).replace("tile: 16", "tile: 1"))

keep("Upper deck", lambda b: drop_component(b, "spriteRenderer"))
deck = new_id("Upper deck")
picture("Deck planks", "Deck.png", 0, 0, deck, order=1)
for i, x in enumerate((6, 88)): picture(f"Deck leg {i + 1}", "DeckLeg.png", x, 10, deck, order=-2)

# The raft picture sits on the shuttle itself: a child of the tweened kinematic body drew ahead of its collider.
keep("Shuttle", lambda b: re.sub(r"  - type: spriteRenderer\n(?:    .*\n)*", sprite("Raft.png", order=1), b, count=1).replace("  height: 14\n", "  height: 17\n").replace("  y: 240\n", "  y: 243\n", 1))
# (3 units below the deck: the runner leaves the deck before landing on the shuttle, instead of riding on its last pixels.)
keep("Landing", lambda b: drop_component(b, "spriteRenderer"))
picture("Pillar", "Pillar.png", 0, 0, new_id("Landing"), order=-1)
picture("Pennant", "Pennant.png", 30, -30, new_id("Landing"), order=1)
keep("Anchor", lambda b: drop_component(b, "spriteRenderer"))
keep("Pendulum", lambda b: re.sub(r"  - type: spriteRenderer\n(?:    .*\n)*", sprite("Ball.png", order=2), b, count=1))
# Beam and post the pendulum hangs from; the anchor (675, 95) sits under the beam.
picture("Beam", "Beam.png", 660, 87, None, order=-1)
picture("Beam post", "BeamPost.png", 766, 90, None, order=-2)
lw, lh = png_size("ChainLink.png")
links = [picture(f"Chain link {i + 1}", "ChainLink.png", 675, 100 + i * 8, None, order=1) for i in range(7)]
obj("Chain", 0, 0, 1, 1, "  - type: script\n    script: ChainLinks\n    values: {" + f"\"Anchor\": \"{new_id('Anchor')}\", \"Bob\": \"{new_id('Pendulum')}\", "
    f"\"LinkSize\": {{\"x\": {lw}, \"y\": {lh}}}, \"Links\": [" + ", ".join(f'"{l}"' for l in links) + "]}\n")

# Torches: one at the foot of the incline, one on the far landing (off the deck, where the guard patrols).
# Fixed post, animated flame, the sample's flickering point light.
light = re.search(r"  - type: pointLight2D\n(?:    .*\n)*", blocks["Torch flame"])[0].replace("flicker: 0.73", "flicker: 0.1").replace("frequency: 1.9", "frequency: 1.2")
tw, th = png_size("TorchPost.png")
for i, (x, ground) in enumerate(((140, 330), (724, 240))):
    torch = obj(f"Torch {i + 1}", x, ground - th, tw, th, sprite("TorchPost.png", order=-3) + light)   # behind the characters
    fw, fh = png_size("TorchFlame1.png")
    obj(f"Torch {i + 1} flame", (tw - fw) // 2, 3 - fh, fw, fh, sprite("TorchFlame1.png", order=-2) + f"  - type: spriteAnimator\n    clip: {flame}\n    playOnStart: true\n", torch)

# Camera follows the runner at 3× (one art pixel = 3 game pixels), close enough to see the feet on the incline. It starts where
# following the runner puts it: parallax layers keep the screen place they had on the first frame.
obj("Camera", 89, 285, 1, 1, f"  - type: camera2D\n    zoom: 3\n    follow: {new_id('Runner')}\n    halfLife: 0.25\n    bounds: {{x: -96, y: 40, width: 896, height: 320}}\n")
keep("Far landing shot")
keep("Walk region")
keep("Lookout", lambda b: drop_component(b, "spriteRenderer"))
gw, gh = png_size("Guard1.png")
obj("Guard", -3, 24 - gh, gw, gh, sprite("Guard1.png", order=3) + f"  - type: spriteAnimator\n    controller: {guard_animator}\n    playOnStart: true\n"
    "  - type: script\n    script: MotionFacing\n    values: {}\n", new_id("Lookout"))

# Ground: 16-unit autotile cells from x -112 to 816 on y 330..394, with a pit under the shuttle's run (x 480..720).
cells = {}
for c in range(58):
    x = -112 + c * 16
    if 480 <= x < 720: continue
    for r in range(4): cells.setdefault(c // 32, []).append(f"{{index: {r * 32 + c % 32}, tile: 1}}")
chunks = "".join(f"    - x: {k}\n      y: 0\n      cells: [{', '.join(v)}]\n" for k, v in sorted(cells.items()))
obj("Ground", -112, 330, 16, 16, f"  - type: tilemap2D\n    tileset: {ground_ts}\n    cellSize: {{width: 16, height: 16}}\n    collisionLayer: 3\n    order: -1\n    chunks:\n{chunks}")
picture("Sign", "Sign.png", 8, 330 - png_size("Sign.png")[1], None, order=-1)

# Sky backing: the sky's top colour fixed to the screen, so a camera that looks above the sky picture (the cutscene's) sees sky.
obj("Sky backing", -400, -200, 1600, 800, '  - type: spriteRenderer\n    sortingLayer: -31\n    squareColor: "#3E2C7C"\n  - type: parallax2D\n    scrollX: 0\n    scrollY: 0\n')
obj("Moon", 170, 222, *png_size("Moon.png"), sprite("Moon.png", sortingLayer=-29) + "  - type: parallax2D\n    scrollX: 0.05\n    scrollY: 0\n")
# Sky: fixed vertically, drifting at 0.15 of the camera, repeated sideways.
sw, sh = png_size("Sky.png")
obj("Sky", -120, 117, sw, sh, sprite("Sky.png", sortingLayer=-30) + "  - type: parallax2D\n    scrollX: 0.15\n    scrollY: 0\n    repeatX: true\n")

# ---- HUD: caption, live status (the sample's Result label), feature tags --------------------------------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {PANEL}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
caption = "Slopes, a moving deck, a guard and a cutscene"
width = 110 + 7.4 * len(caption) + 120
cap = obj("Caption", 12, -12, width, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, canvas)
obj("Caption title", 12, 5, width - 118, 20, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>PlatformerLab</b>   {caption}"\n    size: 15\n', cap)
obj("Caption controls", 12, 25, width - 118, 16, '  - type: uiRect\n  - type: uiText\n    text: "Move  ← →      Jump  Space      Talk  Enter"\n    size: 12\n    color: "#BFD0DC"\n', cap)
for name, x in (("Result", width - 116), ("Readout", 0), ("AI readout", 0)):   # the sample's labels Runner.cs and Lookout.cs write to
    def label(b, x=x, name=name):
        b = re.sub(r"  x: \S+\n  y: \S+\n  width: \S+\n  height: \S+\n", f"  x: {x:g}\n  y: 6\n  width: 108\n  height: 20\n", b)
        b = re.sub(r"parentId: \S+", f"parentId: {old_id('Canvas')}", b).replace(old_id("Canvas"), "CAPTION")
        b = re.sub(r"    size: \d+\n", '    size: 14\n    color: "#FFD84A"\n', b)
        return b if name == "Result" else b.replace(f"  name: {name}\n", f"  name: {name}\n  active: false\n")
    out.append(remap(label(blocks[name])).replace("CAPTION", cap))
tags = [("ledge", "One-way tile", "Jump up through the ledge"), ("slope", "Tile collision", "Each slope tile is a polygon"),
        ("seen", "Navigation 2D", "The guard paths after you"), ("talk", "Cutscene", "Camera cut and dialogue"),
        ("ride", "Kinematic deck", "Carries its rider")]
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, 252, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', canvas)

text = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {uid('new/Camera')}\n"
(ROOT / "Scenes/Showcase.scene").write_text(text)
meta = ROOT / "Scenes/Showcase.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
leftover = [g for g in GUID.findall(text) if g in object_ids]
assert not leftover, f"unmapped ids: {leftover[:3]}"
print(f"wrote Scenes/Showcase.scene with {text.count(chr(10) + '- id: ') + 1} objects")
