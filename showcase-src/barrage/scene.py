# Writes BarrageLab's showcase scene (Scenes/Showcase.scene), its bolt prefab, pellet and bolt sprites and particle assets.
# Run after art.sh:  python3 showcase-src/barrage/scene.py
#
# A side-view shooter at camera zoom 1 (one art pixel = 2 world units): the battleship's core fires the sample's
# SpiralBarrage (one Sprite Pool 2D, B switches batched / per-sprite submit), the ship flies with the arrow keys and
# auto-fires pooled bolts (ShipPilot + the sample's PooledBullet), drones flash and burst when hit (Drone).
import pathlib, struct, uuid, zlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/BarrageLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("0d4f6a52-2a8e-5b8b-9a43-6f3c2b7d1e90")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))
BULLETS_SCENE = "12940974-2dc8-472e-af74-dca5b29fdac6"   # Scenes/PooledBullets.scene (SpiralBarrage's N)


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])   # png pixels = world units here


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def text_asset(name, text):
    (ART / name).write_text(text)
    return asset(name)


def png(name, rows, colours):
    """rows: strings of art pixels ('.' clear, else a key of colours); written at 2 png pixels per art pixel."""
    w, h = len(rows[0]) * 2, len(rows) * 2
    raw = b"".join(b"\x00" + b"".join(bytes(colours.get(c, (0, 0, 0, 0))) * 2 for c in row) for row in rows for _ in (0, 1))
    chunk = lambda k, d: struct.pack(">I", len(d)) + k + d + struct.pack(">I", zlib.crc32(k + d))
    (ART / name).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    return asset(name)


# Pellet: white core, grey rim; the barrage tints each one mint or coral.
png("Pellet.png", [".ab.", "abba", "abba", ".ab."], {"a": (150, 150, 160, 255), "b": (255, 255, 255, 255)})
png("Bolt.png", ["..oyyw.", "royyyww", "..oyyw."], {"r": (200, 70, 60, 255), "o": (255, 140, 60, 255), "y": (255, 210, 80, 255), "w": (255, 246, 210, 255)})

thruster = text_asset("Thruster.particles", """rate: 70
shape: Cone
radius: 3
direction: 180
spread: 16
speed: {min: 70, max: 110}
lifetime: {min: 0.12, max: 0.2}
size: {min: 3, max: 6}
color: {min: "#FFFFFF"}
space: World
blend: Additive
colorOverLifetime:
  colors: [{time: 0, color: "#FFF3B0"}, {time: 0.4, color: "#FF9A3C"}, {time: 1, color: "#B0306A"}]
  alphas: [{time: 0, alpha: 1}, {time: 1, alpha: 0}]
sizeOverLifetime: {keys: [{time: 0, value: 1}, {time: 1, value: 0.3}]}
""")
explosion = text_asset("Explosion.particles", """duration: 0.8
looping: false
rate: 0
bursts: [{time: 0, count: 40}]
maxParticles: 128
shape: Circle
radius: 6
spread: 360
speed: {min: 40, max: 150}
lifetime: {min: 0.35, max: 0.75}
size: {min: 3, max: 7}
color: {min: "#FFFFFF"}
drag: 2
space: World
blend: Additive
colorOverLifetime:
  colors: [{time: 0, color: "#FFF6D0"}, {time: 0.3, color: "#FFB13B"}, {time: 1, color: "#C2307A"}]
  alphas: [{time: 0, alpha: 1}, {time: 0.6, alpha: 0.9}, {time: 1, alpha: 0}]
sizeOverLifetime: {keys: [{time: 0, value: 1}, {time: 1, value: 0.3}]}
""")
# Two star layers drifting left at different speeds (near stars faster and bigger), already full at the first frame.
stars = lambda name, speed, size, rate, colour: text_asset(name, f"""duration: {int(820 / speed) + 1}
prewarm: true
rate: {rate}
maxParticles: 512
shape: Box
width: 4
height: 450
direction: 180
spread: 0
speed: {{min: {speed}, max: {speed}}}
lifetime: {{min: {820 / speed:.2f}, max: {820 / speed:.2f}}}
size: {{min: {size}, max: {size}}}
color: {{min: "{colour}"}}
""")
far_stars = stars("FarStars.particles", 30, 2, 6, "#8C7CC0")
near_stars = stars("NearStars.particles", 110, 2, 8, "#E4DCFF")

bolt_prefab = text_asset("Bolt.prefab", f"""schemaVersion: 42
objects:
- id: {uid('prefab/Bolt')}
  name: Bolt
  groups:
  - Bullets
  width: 14
  height: 6
  components:
  - type: spriteRenderer
    order: 3
    assetId: {asset('Bolt.png')}
    asset: Assets/Showcase/Bolt.png
  - type: circleCollider2D
    isTrigger: true
    layer: 1
    mask: 4
  - type: script
    script: PooledBullet
    values: {{}}
""")

# ---- scene ----------------------------------------------------------------------------------------------------------
out = []
def obj(name, x, y, w, h, comps, parent=None, extra=""):
    out.append(f"- id: {uid('obj/' + name)}\n" + (f"  parentId: {parent}\n" if parent else "") + f"  name: {name}\n{extra}"
               f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
    return uid("obj/" + name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def picture(name, image, x, y, parent=None, comps="", extra="", **kw):
    return obj(name, x, y, *png_size(image), sprite(image, **kw) + comps, parent, extra)


def emitter(name, particles, x, y, parent=None, trigger="Start", order=1):
    return obj(name, x, y, 1, 1, f"  - type: particleEmitter2D\n    particles: {particles}\n    trigger: {trigger}\n    order: {order}\n", parent, extra="  tag: Particles\n")


obj("Space", 0, 0, 800, 450, sprite("Space.png", sortingLayer=-30))
emitter("Far stars", far_stars, 802, 0, order=-20)
emitter("Near stars", near_stars, 802, 0, order=-19)
picture("Asteroid", "Asteroid.png", 230, 318, order=-5)

# Battleship on the right; the spiral emitter sits on its reactor core. Its bullets draw under the ship and drones.
bw, bh = png_size("Boss.png")
boss = picture("Battleship", "Boss.png", 548, 225 - 47, order=0)
canvas_ids = {n: uid("obj/" + n) for n in ("Mode", "Counters")}
# Second attack (Space): a reflecting laser that launches the sample's comets (Comet.prefab, Trail 2D).
laser = obj("Laser", 0, 0, 1, 1, '  - type: line2D\n    points:\n    - {x: 0, y: 0}\n    - {x: 1, y: 0}\n    width: 6\n    startColor: "#FFF6C8"\n    endColor: "#FF3D8B"\n    order: 3\n')
obj("Laser emitter", 548 + 123, 225, 8, 8, f'  - type: script\n    script: BossLaser\n    values: {{"Laser": "{laser}", "Comet": {{"Id": "d994d3ef-42dc-40d8-8f3c-700a4cf14a1a"}}}}\n')
obj("Spiral emitter", 548 + 123, 225, 8, 8, sprite("Pellet.png", order=1)
    + f"  - type: script\n    script: SpiralBarrage\n    values: {{\"Mode\": \"{canvas_ids['Mode']}\", \"Counters\": \"{canvas_ids['Counters']}\", \"Bullets\": {{\"Id\": \"{BULLETS_SCENE}\"}}}}\n")

# Ship: arrow keys, auto-fire, an engine flame.
sw, sh = png_size("Ship.png")
ship = picture("Ship", "Ship.png", 80, 204, order=3,
               comps=f"  - type: script\n    script: ShipPilot\n    values: {{\"Bolt\": {{\"Id\": \"{bolt_prefab}\"}}, \"BoltInfo\": \"{uid('obj/Bolt info')}\", \"Muzzle\": {{\"x\": {sw - 6}, \"y\": {sh // 2 - 3}}}}}\n")
emitter("Engine flame", thruster, 2, sh // 2, ship, order=2)

# Drones between the ship and the battleship.
for i, (image, x, y, amp, period) in enumerate((("DroneA.png", 330, 150, 70, 200), ("DroneB.png", 420, 250, 90, 150), ("DroneA.png", 500, 120, 50, 260))):
    w, h = png_size(image)
    flash = uid(f"obj/Drone {i + 1} flash")
    burst = uid(f"obj/Drone {i + 1} burst")
    drone = picture(f"Drone {i + 1}", image, x, y, order=2, comps="  - type: boxCollider2D\n    isTrigger: true\n    layer: 2\n    mask: 2\n"
                    f"  - type: script\n    script: Drone\n    values: {{\"Amplitude\": {amp}, \"Period\": {period}, \"Flash\": \"{flash}\", \"Burst\": \"{burst}\"}}\n")
    picture(f"Drone {i + 1} flash", image.replace(".png", "Flash.png"), 0, 0, drone, extra="  active: false\n", order=3)
    emitter(f"Drone {i + 1} burst", explosion, w // 2, h // 2, drone, trigger="Manual", order=5)

obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")

# ---- HUD: caption, live barrage counters (SpiralBarrage writes Mode and Counters), feature tags ---------------------
canvas = obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
panel = f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
caption = "Bullet hell from one sprite pool and a prefab pool"
width = 110 + 7.4 * len(caption)
cap = obj("Caption", 12, -12, width, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel, canvas)
obj("Caption title", 12, 5, width - 24, 20, f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>BarrageLab</b>   {caption}"\n    size: 15\n', cap)
obj("Caption controls", 12, 25, width - 24, 16, '  - type: uiRect\n  - type: uiText\n    text: "Move  ↑ ↓ ← →      Batching  B      Laser  Space      Pool  P"\n    size: 12\n    color: "#BFD0DC"\n', cap)
stats = obj("Barrage panel", -12, -12, 290, 44, "  - type: uiRect\n    anchor: BottomRight\n" + panel, canvas)
bolts = obj("Bolt panel", -12, -60, 290, 24, "  - type: uiRect\n    anchor: BottomRight\n" + panel, canvas)
obj("Bolt info", 10, 4, 274, 16, '  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>Bolts</b>   Rent / Return"\n    size: 12\n    color: "#F4F6F4"\n', bolts)
obj("Mode", 12, 5, 266, 20, '  - type: uiRect\n  - type: uiText\n    text: "Batched submit"\n    size: 14\n    color: "#FFD84A"\n', stats)
obj("Counters", 12, 25, 266, 16, '  - type: uiRect\n  - type: uiText\n    text: "Active 0"\n    size: 12\n    color: "#BFD0DC"\n', stats)
tags = [("barrage", "Sprite Pool 2D", "Thousands of bullets, one batched draw"), ("bolts", "Prefab pool", "Bolts are rented and returned, not allocated"),
        ("burst", "Particles", "Drones flash and burst when hit"), ("compare", "Per-sprite submit", "B switches batching: watch the draw count"),
        ("laser", "Line 2D + Trail 2D", "A reflecting laser launches comets with trails")]
tag_w = 24 + 6.6 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel
        + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', canvas)

text = "schemaVersion: 42\nobjects:\n" + "".join(out) + f"defaultCameraId: {uid('obj/Camera')}\n"
(ROOT / "Scenes/Showcase.scene").write_text(text)
meta = ROOT / "Scenes/Showcase.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene')}\n")
print(f"wrote Scenes/Showcase.scene with {len(out)} objects; scene id {meta.read_text().split()[1]}")
