# Writes the KeyRoom showcase: tilesets, clips, particles, sidecar metas, blob shadows and the two scenes
# Scenes/Showcase.scene (key, chest, door) and Scenes/ShowcaseRoom2.scene (crate and pressure plate).
# Run after art.sh:  python3 showcase-src/keyroom/scene.py   (from anywhere; paths are relative to this file)
# Game logic is the sample's own scripts (KeySequence, DoorLock, OpenChest, RoomExit, PushCrate, PressurePlate, Checkpoint);
# only HeroAnimation, FlagFx and DoorwayFade are new.
# The project turns on integer scale (GameSettings) so every art pixel is exactly 3 game pixels and 9 or so device pixels.
import pathlib, random, struct, uuid, zlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/KeyRoom"
ART = ROOT / "Assets/Showcase"
T = 32                      # one tile in world units (16 art pixels at 2 px each)
COLS, ROWS = 18, 10         # room size in tiles; the camera at zoom 1.5 shows 16.7 × 9.4 of them, so the outer walls run off the edges
DOOR_COLS = (8, 9)          # the two-tile gap in the north wall behind the door, wide enough to walk into without lining up
uid = lambda name: str(uuid.uuid5(uuid.UUID("64fb4cd6-2469-5cec-ba6d-9a49e61f42e7"), "showcase/" + name))
oid = lambda name: uid("obj/" + scene_name + "/" + name)
PANEL = "8417743f-7aed-49af-becf-fce814d4d466"       # Assets/GuideFrame.png, the sample's 9-slice panel
RULES, LOOT_TABLE = "8fe7265d-b167-4261-91e8-c388d8a4fc16", "24aec28b-0157-450e-b4c8-b65e7f77bb54"
UNLOCK_WAV = "9741ea19-cad2-5668-ae72-fadf43b42ca2"


def png_size(path):
    w, h = struct.unpack(">II", path.read_bytes()[16:24])
    return w, h


def asset(name):  # id of Assets/Showcase/<name>, writing its sidecar meta once
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def text_asset(name, text):
    (ART / name).write_text(text)
    return asset(name)


def shadow(name, w, h):  # hard-edged oval, 2 px per art pixel like everything else
    px = lambda x, y: (23, 15, 48, 120) if ((x + .5) / w * 2 - 1) ** 2 + ((y + .5) / h * 2 - 1) ** 2 <= 1 else (0, 0, 0, 0)
    rows = [[px(x // 2, y // 2) for x in range(w * 2)] for y in range(h * 2)]
    raw = b"".join(b"\x00" + bytes(c for p in row for c in p) for row in rows)
    chunk = lambda kind, data: struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    (ART / name).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w * 2, h * 2, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def tileset(name, image, solid):
    w, h = png_size(ART / image)
    tiles = "".join(f"- id: {y // T * (w // T) + x // T + 1}\n  source:\n    x: {x}\n    y: {y}\n    width: {T}\n    height: {T}\n  solid: {str(solid).lower()}\n"
                    for y in range(0, h, T) for x in range(0, w, T))
    return text_asset(name, f"image: {asset(image)}\ntileSize:\n  width: {T}\n  height: {T}\nmargin: 0\nspacing: 0\ntiles:\n{tiles}nextId: {w // T * (h // T) + 1}\n"), w // T


def clip(name, frames, seconds, loop=True):
    return text_asset(name, "frames:\n" + "".join(f"- sprite: {asset(f)}\n  seconds: {seconds:g}\n" for f in frames) + f"loop: {str(loop).lower()}\n")


# ---- scene writer -------------------------------------------------------------------------------------------------
def obj(name, x=None, y=None, w=None, h=None, parent=None, comps="", extra=""):
    out.append(f"- id: {oid(name)}\n" + (f"  parentId: {oid(parent)}\n" if parent else "") + f"  name: {name}\n" + extra
               + "".join(f"  {k}: {v:g}\n" for k, v in (("x", x), ("y", y), ("width", w), ("height", h)) if v is not None)
               + ("  components:\n" + comps if comps else "  components: []\n"))   # an empty key does not parse


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


def script(name, values=""):
    return f"  - type: script\n    script: {name}\n" + (f"    values:\n{values}" if values else "    values: {}\n")


def light(radius, intensity, flicker, hz, color):
    return f'  - type: pointLight2D\n    radius: {radius}\n    intensity: {intensity}\n    flicker: {flicker}\n    frequency: {hz:g}\n    color: "{color}"\n'


def emitter(particles, trigger, layer=0, order=3):
    return f"  - type: particleEmitter2D\n    particles: {particles}\n    trigger: {trigger}\n    seed: 0\n    offsetX: 0\n    offsetY: 0\n    sortingLayer: {layer}\n    order: {order}\n"


GROUP = "  - type: sortingGroup2D\n    sortByY: true\n"
SOLID = "  - type: boxCollider2D\n"
TRIGGER = "  - type: boxCollider2D\n    isTrigger: true\n"


def tilemap(name, ts, cells, order):
    body = "".join(f"      - index: {r * 32 + c}\n        tile: {t}\n" for (c, r), t in sorted(cells.items(), key=lambda i: (i[0][1], i[0][0])))
    obj(name, 0, 0, T, T, comps=f"  - type: tilemap2D\n    tileset: {ts}\n    cellSize:\n      width: {T}\n      height: {T}\n    sortingLayer: 0\n    order: {order}\n    chunks:\n    - x: 0\n      y: 0\n      cells:\n{body}")


def shade(name, image, left, bottom, width, parent=None):
    sw, sh = png_size(ART / image)
    obj(name + " shadow", left + (width - sw) / 2, bottom - sh * .6, sw, sh, parent=parent, comps=sprite(image, order=-4))


def prop(name, image, left, bottom, shadow_image="ShadowL.png"):
    """A Y-sorted prop standing with its bottom edge at `bottom`; its collider is the lower part only so the hero can walk behind it."""
    w, h = png_size(ART / image)
    shade(name, shadow_image, left, bottom, w)
    obj(name, left, bottom - h, w, h, comps=sprite(image, sortByY="true"))
    obj(name + " base", 2, h - 14, w - 4, 12, parent=name, comps=SOLID)


def room(seed):
    """Floor, walls with a one-tile doorway in the north wall, and two torches."""
    rng = random.Random(seed)
    special = [r * 8 + c + 1 for c, r in ((4, 0), (1, 1), (7, 2), (1, 4), (4, 5), (2, 6))]   # mossy stones on the 8×8 sheet
    cracked = [r * 8 + c + 1 for c, r in ((5, 1), (0, 3), (3, 4), (5, 6))]                    # left out: they read as scribbles
    plain = [i for i in range(1, 65) if i not in special + cracked]
    tilemap("Floor", floor_ts, {(c, r): rng.choice(special) if rng.random() < .05 else rng.choice(plain) for c in range(1, COLS - 1) for r in range(3, ROWS - 1)}, -10)
    face = lambda c, r: (r - 1) * face_cols + (c - 1) % face_cols + 1
    tilemap("Wall face", face_ts, {(c, r): face(c, r) for c in range(1, COLS - 1) for r in (1, 2) if c not in DOOR_COLS}, -9)
    # The doorway keeps its bricks (they show around the arch) from a copy of the tileset without collision.
    tilemap("Wall face behind door", face_back_ts, {(c, r): face(c, r) for c in DOOR_COLS for r in (1, 2)}, -9)
    tilemap("Wall cap", cap_ts, {(c, r): rng.randint(1, cap_cols) for c in range(COLS) for r in range(ROWS) if r in (0, ROWS - 1) or c in (0, COLS - 1)}, -8)
    # No Global Light 2D: the torches then add warm light on top of the art. With one, lit stone only returns to its own colour.
    w, h = png_size(ART / "TorchSconce.png")
    fw, fh = png_size(ART / "TorchFlame1.png")
    for i, x in enumerate((160, 416)):
        obj(f"Torch {i + 1}", x - w / 2, 66, w, h, comps=sprite("TorchSconce.png", sortingLayer=5) + light(150, 0.75, 0.1, 1.1 + i * .3, "#FF7418"))
        obj(f"Torch {i + 1} flame", (w - fw) / 2, -fh, fw, fh, parent=f"Torch {i + 1}",
            comps=sprite("TorchFlame1.png", sortingLayer=5, order=1) + f"  - type: spriteAnimator\n    clip: {flame_clip}\n    playOnStart: true\n")
        obj(f"Torch {i + 1} embers", w / 2, -fh + 8, 1, 1, parent=f"Torch {i + 1}", comps=emitter(embers, "start", layer=5))


def hero(x, y):
    """The root is the feet box (collider, movement, Y-sort); the animated picture hangs above it."""
    w, h = png_size(ART / "HeroIdleS.png")
    obj("Player", x, y, 22, 12, comps=GROUP + script("PlayerMovement") + SOLID)
    obj("Hero body", 0, 0, 22, 12, parent="Player", comps="")   # what a doorway hides
    sw, sh = png_size(ART / "ShadowM.png")
    obj("Hero shadow", (22 - sw) / 2, 12 - sh * .7, sw, sh, parent="Hero body", comps=sprite("ShadowM.png", order=-1))
    obj("Hero", (22 - w) / 2, 14 - h, w, h, parent="Hero body",
        comps=sprite("HeroIdleS.png", order=0) + f"  - type: spriteAnimator\n    controller: {hero_animator}\n    playOnStart: true\n" + script("HeroAnimation"))


def door(name, comps):
    w, h = png_size(ART / "Door.png")
    obj(name, COLS * T / 2 - w / 2, 3 * T - h, w, h, comps=comps)
    obj(name + " back wall", DOOR_COLS[0] * T, T, 2 * T, T, comps=SOLID)   # the hero steps into the doorway and no further


def hud(caption, second_line_name, second_line, tags):
    width = 110 + 6.6 * len(caption) + 96   # the last 96 units hold the save status
    obj("Interface", comps="  - type: uiCanvas\n")
    panel = f"  - type: uiImage\n    sprite: {PANEL}\n    borders: {{left: 8, top: 8, right: 8, bottom: 8}}\n"
    obj("Caption", 12, -12, width, 44, parent="Interface", comps="  - type: uiRect\n    anchor: BottomLeft\n" + panel)
    obj("Caption title", 12, 5, width - 118, 20, parent="Caption", comps=f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "<b>KeyRoom</b>   {caption}"\n    size: 15\n')
    obj(second_line_name, 12, 25, width - 118, 16, parent="Caption", comps=f'  - type: uiRect\n  - type: uiText\n    text: "{second_line}"\n    size: 12\n    color: "#BFD0DC"\n')
    # Icons are drawn at 1.5× so their pixels match the world's under the 1.5× camera.
    obj("Key icon", 14, 12, 45, 27, parent="Interface", comps=f"  - type: uiRect\n    showIf: key\n  - type: uiImage\n    sprite: {asset('Key.png')}\n")
    obj("Coin icon", 68, 15, 21, 21, parent="Interface", comps=f"  - type: uiRect\n    showIf: coin\n  - type: uiImage\n    sprite: {asset('Coin.png')}\n")
    # Feature tags, top right: each names the engine feature a beat just used and appears with that beat's session flag.
    for i, (flag, feature, detail) in enumerate(tags):
        show = f"    showIf: {flag}\n" if flag else ""
        obj(f"Tag {i + 1}", -12, 12 + i * 28, 236, 24, parent="Interface", comps="  - type: uiRect\n    anchor: TopRight\n" + show + panel
            + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n')
    obj("Save status", width - 100, 13, 92, 20, parent="Caption", comps='  - type: uiRect\n  - type: uiText\n    text: ""\n    size: 13\n    color: "#FFD84A"\n' + script("Checkpoint"))


def fx(name, flag, emit=None, shake=0.0, punch=0.0, show=None, delay=0.0):
    values = f'      Flag: "{flag}"\n' + (f'      Emitter: "{oid(emit)}"\n' if emit else "") + (f'      Show: "{oid(show)}"\n' if show else "") + f"      Shake: {shake:g}\n      Punch: {punch:g}\n      Delay: {delay:g}\n"
    obj(name, 0, 0, 1, 1, comps=script("FlagFx", values))


def write(file):
    (ROOT / "Scenes" / file).write_text("schemaVersion: 42\nobjects:\n" + "".join(out))
    meta = ROOT / "Scenes" / (file + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid('scene/' + file)}\n")
    print(f"wrote Scenes/{file} with {len(out)} objects")
    return meta.read_text().split()[1]


# ---- shared assets ------------------------------------------------------------------------------------------------
for n, (w, h) in {"ShadowM.png": (16, 5), "ShadowL.png": (22, 6)}.items(): shadow(n, w, h)
floor_ts, _ = tileset("Floor.tileset", "Floor.png", False)
face_ts, face_cols = tileset("WallFace.tileset", "WallFace.png", True)
face_back_ts, _ = tileset("WallFaceBack.tileset", "WallFace.png", False)
cap_ts, cap_cols = tileset("WallCap.tileset", "WallCap.png", True)
flame_clip = clip("Flame.clip", ["TorchFlame1.png", "TorchFlame2.png", "TorchFlame1.png", "TorchFlame3.png"], 0.16)
# The door holds shut for 0.5 s so the camera has reached it before it swings open (FlagFx shakes at the same moment).
DOOR_DELAY = 0.5
door_clip = text_asset("DoorOpen.clip", f"frames:\n- sprite: {asset('Door.png')}\n  seconds: {DOOR_DELAY}\n- sprite: {asset('DoorOpen.png')}\n  seconds: 0.1\nloop: false\n")
# Particle squares are 2 or 4 world units: one or two art pixels.
embers = text_asset("Embers.particles", 'rate: 7\nshape: Box\nwidth: 8\nheight: 2\nspread: 30\nspeed: {min: 10, max: 26}\nlifetime: {min: 0.7, max: 1.4}\nsize: {min: 2, max: 2}\n'
                    'color: {min: "#FF9A3C", max: "#FFE08A"}\ngravity: -14\nblend: Additive\n')
door_dust = text_asset("DoorDust.particles", 'duration: 0.6\nlooping: false\nrate: 0\nbursts: [{time: 0, count: 34}]\nshape: Box\nwidth: 56\nheight: 4\nspread: 150\nspeed: {min: 15, max: 55}\n'
                      'lifetime: {min: 0.5, max: 1.1}\nsize: {min: 2, max: 4}\ncolor: {min: "#6E6881", max: "#D8D2E8"}\nendSize: 0.3\ngravity: -8\n')
sparkle = text_asset("Sparkle.particles", 'duration: 0.5\nlooping: false\nrate: 0\nbursts: [{time: 0, count: 26}]\nspread: 360\nspeed: {min: 30, max: 110}\nlifetime: {min: 0.35, max: 0.8}\n'
                     'size: {min: 2, max: 4}\ncolor: {min: "#FFFFFF", max: "#FFD84A"}\nendSize: 0.5\nblend: Additive\n')
# Facing sets are listed N, E, S, W and the state clip must be the first of them. Left-facing frames are the right-facing ones mirrored (art.sh).
idle = [clip(f"HeroIdle{d}.clip", [f"HeroIdle{d}.png"], 1) for d in "NESW"]
walk = [clip(f"HeroWalk{d}.clip", [f"HeroWalk{d}{i}.png" for i in (1, 2, 3, 4)], 0.13) for d in "NESW"]
states = lambda name, clips: f"- name: {name}\n  clip: {clips[0]}\n  directions:\n" + "".join(f"  - {c}\n" for c in clips) + "  direction: Move\n"
hero_animator = text_asset("Hero.animator", "entry: Idle\nstates:\n" + states("Idle", idle) + states("Walk", walk)
                           + "parameters:\n- name: Move\n  kind: Vector2\n  default: 0\n- name: Speed\n  kind: Float\n  default: 0\n"
                           "transitions:\n- from: Idle\n  to: Walk\n  parameter: Speed\n  test: Greater\n  value: 0.1\n- from: Walk\n  to: Idle\n  parameter: Speed\n  test: AtMost\n  value: 0.1\n")
w, h = png_size(ART / "CoinPile.png")
coin_prefab = text_asset("Coin.prefab", f"schemaVersion: 27\nobjects:\n- id: {uid('prefab/coin')}\n  name: Coin\n  tag: Coin\n  groups:\n  - Collectibles\n  width: {w}\n  height: {h}\n  components:\n"
                         + sprite("CoinPile.png", order=2) + TRIGGER + script("CollectCoin", f'      LootTable:\n        Id: "{LOOT_TABLE}"\n'))

# ---- room 2: push the crate onto the plate, then stand on it too --------------------------------------------------------
scene_name, out = "room2", []
room(11)
door("Pressure Door", SOLID + sprite("Door.png", order=-5))
pw, ph = png_size(ART / "Plate.png")
plate_w, plate_x, plate_y = 104, 296, 176     # the plate is 9-sliced wider than its picture so the crate and the hero both fit
obj("Pressure Plate", plate_x, plate_y, plate_w, ph, comps=sprite("Plate.png", order=-6, drawMode="Sliced", slice="{left: 10, top: 8, right: 10, bottom: 8}") + TRIGGER + script("PressurePlate",
    f'      Player: "{oid("Player")}"\n      Crate: "{oid("Push Crate")}"\n      Door: "{oid("Pressure Door")}"\n      Label: "{oid("Plate status")}"\n'
    f'      ClosedSprite:\n        Id: "{asset("Door.png")}"\n      OpenSprite:\n        Id: "{asset("DoorOpen.png")}"\n'))
cw, ch = png_size(ART / "PushCrate.png")
crate_y = plate_y + ph / 2 - 8     # the crate's base box runs along the middle of the plate
obj("Push Crate", 206, crate_y, cw, 16, comps=GROUP + SOLID + script("PushCrate", f'      Player: "{oid("Player")}"\n'))
obj("Push Crate picture", 0, 16 - ch, cw, ch, parent="Push Crate", comps=sprite("PushCrate.png"))
sw, sh = png_size(ART / "ShadowL.png")
obj("Push Crate shade", (cw - sw) / 2, 16 - sh * .6, sw, sh, parent="Push Crate", comps=sprite("ShadowL.png", order=-1))
prop("Barrel", "Barrel.png", 472, 128)
prop("Pot 1", "Pot.png", 44, 126)
prop("Pot 2", "PotSmall.png", 80, 132, "ShadowM.png")
prop("Crate", "Crate.png", 476, 276)
hero(136, crate_y + 2)
obj("Doorway", DOOR_COLS[0] * T + 4, 3 * T - 16, 2 * T - 8, 12, comps=TRIGGER + script("DoorwayFade", f'      Player: "{oid("Player")}"\n      Hide: "{oid("Hero body")}"\n      FadeOut: true\n'))
obj("Main Camera", COLS * T / 2, ROWS * T / 2, 40, 40, comps="  - type: camera2D\n    zoom: 1.5\n")
hud("Push the crate onto the plate, then step on it too", "Plate status", "Plate 0/2  Empty  |  Door CLOSED",
    [(None, "Scene load", "Room 1 → Room 2"), (None, "Physics push", "Crate + trigger plate")])
room2 = write("ShowcaseRoom2.scene")

# ---- room 1: key, chest, door ---------------------------------------------------------------------------------------
scene_name, out = "room1", []
room(7)
obj("Rug", 388, 222, *png_size(ART / "Rug.png"), comps=sprite("Rug.png", order=-6))
door("Door", script("DoorLock") + SOLID + sprite("Door.png", order=-5) + f"  - type: spriteAnimator\n    clip: {door_clip}\n    playOnStart: false\n  - type: audioSource\n    sound: {UNLOCK_WAV}\n")
obj("Exit", DOOR_COLS[0] * T + 4, 3 * T - 16, 2 * T - 8, 12, comps=TRIGGER + script("RoomExit", f'      Player: "{oid("Player")}"\n      Destination:\n        Id: "{room2}"\n')
    + script("DoorwayFade", f'      Player: "{oid("Player")}"\n      Hide: "{oid("Hero body")}"\n      FadeOut: false\n'))   # no fade here: a fade outlives the scene change

prop("Barrel 1", "Barrel.png", 42, 124)
prop("Barrel 2", "Barrel.png", 78, 132)
prop("Crate", "Crate.png", 444, 126)
prop("Pot 1", "Pot.png", 482, 136)
prop("Pot 2", "Pot.png", 44, 250)
prop("Pot 3", "PotSmall.png", 84, 258, "ShadowM.png")
prop("Crate 2", "Crate.png", 452, 286)
prop("Barrel 3", "Barrel.png", 494, 288)

# Chest: the body has no picture. "Chest Lid" (the closed chest) is what OpenChest hides; "Chest open" starts off and
# FlagFx switches it on with the chest flag. Both stand bottom-centred in the body box.
ow, oh = png_size(ART / "ChestOpen.png")
lw, lh = png_size(ART / "Chest.png")
cw2, ch2, cx, cb = max(ow, lw), max(oh, lh), 364, 214
shade("Chest", "ShadowL.png", cx, cb, cw2)
obj("Chest", cx, cb - ch2, cw2, ch2, comps=GROUP + script("OpenChest",
    f'      Player: "{oid("Player")}"\n      Lid: "{oid("Chest Lid")}"\n      Rules:\n        Id: "{RULES}"\n      SaveProgress: true\n      Loot:\n        Id: "{coin_prefab}"\n'))
obj("Chest Lid", (cw2 - lw) / 2, ch2 - lh, lw, lh, parent="Chest", comps=sprite("Chest.png", order=1))
obj("Chest open", (cw2 - ow) / 2, ch2 - oh, ow, oh, parent="Chest", comps=sprite("ChestOpen.png", order=1), extra="  active: false\n")
obj("Chest base", 2, ch2 - 14, cw2 - 4, 12, parent="Chest", comps=SOLID)
obj("Chest sparkle", cw2 / 2, ch2 / 2, 1, 1, parent="Chest", comps=emitter(sparkle, "manual", layer=5))

# Key: the trigger stays; KeySequence hides the picture (and with it the glow and the shadow).
kw, kh = png_size(ART / "Key.png")
obj("Key", 168, 186, kw, kh, comps=TRIGGER + f"  - type: script\n    script: KeySequence\n    eventLinks:\n    - event: KeyAcquired\n      target: {oid('Door')}\n      script: DoorLock\n      method: Unlock\n    values:\n"
    f'      Collector: "{oid("Player")}"\n      KeyImage: "{oid("Key Image")}"\n      Frames:\n      - "{asset("DoorOpen.png")}"\n      DoorCamera: "{oid("Door Camera")}"\n      PlayerCamera: "{oid("Main Camera")}"\n      HoldSeconds: 1.4\n')
obj("Key Image", 0, 0, kw, kh, parent="Key", comps=sprite("Key.png", sortByY="true") + light(44, 0.5, 0.35, 1.1, "#FFE27A"))
sw, sh = png_size(ART / "ShadowM.png")
obj("Key shade", (kw - sw) / 2, kh - sh * .4, sw, sh, parent="Key Image", comps=sprite("ShadowM.png", order=-4))

hero(277, 196)
obj("Main Camera", COLS * T / 2, ROWS * T / 2, 40, 40, comps="  - type: camera2D\n    zoom: 1.5\n")
obj("Door Camera", COLS * T / 2, 2.6 * T, 40, 40, comps="  - type: camera2D\n    zoom: 3\n")
obj("Door dust", COLS * T / 2, 3 * T - 6, 1, 1, comps=emitter(door_dust, "manual", layer=5))
fx("Door feedback", "key", emit="Door dust", shake=3, delay=DOOR_DELAY)
fx("Chest feedback", "chest", emit="Chest sparkle", punch=0.05, show="Chest open")
hud("A small game in a handful of scripts", "Controls", "Move  WASD / arrows      Open  E      Save  F5",
    [("key", "Event link", "Key → Door.Unlock"), ("chest", "Prefab spawn", "Chest → Coins"), ("coin", "Trigger pickup", "Coin → HUD")])
write("Showcase.scene")
