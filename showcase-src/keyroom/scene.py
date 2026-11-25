# Writes the KeyRoom showcase: tilesets, clips, particles, sidecar metas, blob shadows and the two scenes
# Scenes/Showcase.scene (key, chest, door) and Scenes/ShowcaseRoom2.scene (crate and pressure plate).
# Run after art.sh:  python3 showcase-src/keyroom/scene.py   (from anywhere; paths are relative to this file)
# Game logic is the sample's own scripts (KeySequence, DoorLock, OpenChest, RoomExit, PushCrate, PressurePlate, Checkpoint);
# only HeroAnimation, FlagFx and DoorwayFade are new.
# The project turns on integer scale (GameSettings) so every art pixel is exactly 3 game pixels and 9 or so device pixels.
# The rooms also carry what the retired KeyRoom, SecondRoom and PointerWorkshop scenes showed (user decision 2026-11-26, the
# showcase is the sample): the bag with its compass dialog, the Controls panel, language and pause, the satchel tween, the
# lid and coin component keys, pot prefab instances, mouse-dragged crates and an F8 detail camera. Those objects keep the
# retired scenes' ids (FIXED). The two UI scenes the rooms open, ShowcaseGuide (M) and ShowcaseBindings (K in
# ShowcaseMoves), are written by hand.
import hashlib, pathlib, random, struct, uuid, zlib

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/KeyRoom"
ART = ROOT / "Assets/Showcase"
T = 32                      # one tile in world units (16 art pixels at 2 px each)
COLS, ROWS = 18, 10         # room size in tiles; the camera at zoom 1.5 shows 16.7 × 9.4 of them, so the outer walls run off the edges
DOOR_COLS = (8, 9)          # the two-tile gap in the north wall behind the door, wide enough to walk into without lining up
uid = lambda name: str(uuid.uuid5(uuid.UUID("64fb4cd6-2469-5cec-ba6d-9a49e61f42e7"), "showcase/" + name))
FIXED = {   # ids carried over from Scenes/KeyRoom.scene (room1), SecondRoom.scene (room2) and PointerWorkshop.scene
    "room1": {"Satchel": "51c1580c-0444-5d49-84bc-ad7df3a3add4", "Satchel flap": "b6d5d8c0-a8ea-5c41-8b0e-af85769fea68",
              "Satchel clasp": "a766bff7-7292-5f98-8e01-c6ce52d95fbc", "Pot": "58456987-ba60-5c3d-82fd-501ff396ec74",
              "Small pot": "5d0f8b8a-8d97-4fc9-b807-19318affa919", "Detail Camera": "13bae7c5-4756-5946-b6f2-760f128c34e2",
              "Controls button": "82979000-dcbc-443e-baa1-60ada0400003", "Controls panel": "8fcb65a1-aaee-59d8-82cc-1f9a3148b9a3",
              "Controls hint": "82979000-dcbc-443e-baa1-60ada0400004", "Checkpoint controls": "82979000-dcbc-443e-baa1-60ada0400007",
              "Door hint": "6d1f3a52-0b7e-4c19-9e2d-5a8b7c4e1f20", "Pause indicator": "82979000-dcbc-443e-baa1-60ada0400005",
              "Bag button": "9ef4faec-37e6-5dea-bd27-be9e4f573dee", "Compass bearing": "d52a177a-7bb8-5476-aae2-d41591bb0bfe",
              "Inventory": "81219816-44aa-59c3-97aa-2346740f68c3", "Inventory details": "b9bb97a9-e5ad-42d8-ae63-4a3a42677052",
              "Inventory item": "cbe0f906-a75c-5c2a-8aae-8b99fb2883c9", "Key quest": "9e77730f-bb5a-5e3e-a533-63d4ff4bb294",
              "Manage compass": "d948ac49-b490-51a5-8d7a-4ad5024c7fa0", "Close inventory": "7ede8c1f-280e-5b28-a233-919a040f8cab",
              "Compass confirmation": "78501a61-6ef7-5566-aa40-7ea88ed55f9a", "Compass question": "bd029c08-06dd-59ad-8fd6-eb3f1a369fa7",
              "Apply compass": "9b97ca2b-1584-5cd8-9f40-e670c8189b71", "Cancel compass": "3cc78ff7-0243-5079-a571-3c5c546a65f7",
              "Language": "2b2c92d0-1e21-5077-820e-3364ee5484b4", "Bag language": "805b7643-0644-5aaf-96ee-e6d1f6797699",
              "Confirm language": "c514b1c6-a8cf-5d57-84f5-47ec5e984f6a"},
    "room2": {"Controls button": "82979001-dcbc-443e-baa1-60ada0400003", "Controls panel": "349ed173-26c3-5125-ad86-0302431f15a9",
              "Controls hint": "82979001-dcbc-443e-baa1-60ada0400004", "Checkpoint controls": "82979001-dcbc-443e-baa1-60ada0400007",
              "Pause indicator": "82979001-dcbc-443e-baa1-60ada0400005", "Language": "3e528258-504b-5a95-8345-c3a4f2fa8642"},
}
oid = lambda name: FIXED.get(scene_name, {}).get(name) or uid("obj/" + scene_name + "/" + name)
GUIDE = "25fc2224-7695-4e19-a05c-d8524a2ffe50"      # Scenes/ShowcaseGuide.scene, the additive UI scene M opens
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


def write_png(name, w, h, px):  # px(x, y) gives one art pixel's RGBA; written at 2 px per art pixel like everything else
    rows = [[px(x // 2, y // 2) for x in range(w * 2)] for y in range(h * 2)]
    raw = b"".join(b"\x00" + bytes(c for p in row for c in p) for row in rows)
    chunk = lambda kind, data: struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    (ART / name).write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w * 2, h * 2, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def shadow(name, w, h):  # hard-edged oval
    write_png(name, w, h, lambda x, y: (23, 15, 48, 120) if ((x + .5) / w * 2 - 1) ** 2 + ((y + .5) / h * 2 - 1) ** 2 <= 1 else (0, 0, 0, 0))


def pixels(name, rows, colors):  # small pixel art from a character grid; "." is transparent
    hexrgb = lambda c: tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) + (255,)
    write_png(name, len(rows[0]), len(rows), lambda x, y: (0, 0, 0, 0) if rows[y][x] == "." else hexrgb(colors[rows[y][x]]))


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


def prop(name, image, left, bottom, shadow_image="ShadowL.png", drag=None):
    """A Y-sorted prop standing with its bottom edge at `bottom`; its collider is the lower part only so the hero can walk behind it.
    A draggable prop (drag = the readout widget) carries DragCrate, and its shadow rides along as a child."""
    w, h = png_size(ART / image)
    if drag:
        obj(name, left, bottom - h, w, h, comps=sprite(image, sortByY="true") + script("DragCrate", f'      Label: "{name}"\n      Readout: "{oid(drag)}"\n'))
        shade(name, shadow_image, 0, h, w, parent=name)
    else:
        shade(name, shadow_image, left, bottom, w)
        obj(name, left, bottom - h, w, h, comps=sprite(image, sortByY="true"))
    obj(name + " base", 2, h - 14, w - 4, 12, parent=name, comps=SOLID)


def pot_prefab():
    """Assets/Showcase/Pot.prefab: one Y-sorted pot picture. Returns (asset id, revision, root id, baseline YAML for an instance)."""
    w, h = png_size(ART / "Pot.png")
    root = uid("prefab/pot")
    body = lambda indent, oid_, name, w, h, image: (
        f'{indent}id: {oid_}\n{indent}name: {name}\n{indent}x: 0\n{indent}y: 0\n{indent}width: {w}\n{indent}height: {h}\n{indent}components:\n'
        f'{indent}- type: {q}spriteRenderer{q}\n{indent}  sortingLayer: 0\n{indent}  order: 0\n{indent}  sortByY: true\n{indent}  footOffset: 0\n'
        f'{indent}  flipX: false\n{indent}  flipY: false\n{indent}  pixelSnap: false\n{indent}  squareColor: "#FFFFFF"\n'
        f'{indent}  assetId: {q}{asset(image)}{q}\n{indent}  asset: {q}Assets/Showcase/{image}{q}\n'
        f'{indent}rotation: 0\n{indent}scaleX: 1\n{indent}scaleY: 1\n{indent}pivotX: 0\n{indent}pivotY: 0\n{indent}shear: 0\n')
    q = ""
    text_asset("Pot.prefab", "schemaVersion: 42\nobjects:\n- " + body("  ", root, "Pot", w, h, "Pot.png")[2:])
    revision = hashlib.sha256((ART / "Pot.prefab").read_bytes()).hexdigest().upper()   # the manifest hash: no nested prefabs
    q = '"'
    baseline = lambda instance: body("        ", f'"{instance}"', '"Pot"', w, h, "Pot.png")
    return asset("Pot.prefab"), revision, root, baseline


def pot(name, image, left, bottom, shadow_image, link):
    """An instance of Pot.prefab. Instances carry their link and the source snapshot; the collider is a separate object."""
    prefab, revision, root, baseline = link
    w, h = png_size(ART / image)
    shade(name, shadow_image, left, bottom, w)
    me = oid(name)
    obj(name, left, bottom - h, w, h, comps=sprite(image, sortByY="true"),
        extra=f"  prefab:\n    assetId: {prefab}\n    instances:\n      {root}: {me}\n    revision: {revision}\n    baseline:\n      {root}:\n" + baseline(me))
    obj(name + " base", left + 2, bottom - 14, w - 4, 12, comps=SOLID)


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
    obj("Save status", width - 100, 13, 92, 20, parent="Caption", comps='  - type: uiRect\n  - type: uiText\n    text: ""\n    size: 13\n    color: "#FFD84A"\n' + script("Checkpoint")
        + script("RoomGuide", f'      Guide:\n        Id: "{GUIDE}"\n'))


def ui_text(text, key=None, size=14, inset=(10, 4), wrap=False, color=None):
    return ('  - type: uiText\n' + ("    wrap: true\n" if wrap else "") + f'    text: "{text}"\n' + (f"    textKey: {key}\n" if key else "")
            + f"    size: {size}\n" + (f'    color: "{color}"\n' if color else "") + f"    insetX: {inset[0]}\n    insetY: {inset[1]}\n")


def button(name, x, width, text, key, behaviour, anchor="BottomRight", y=-20, parent="Interface"):
    """A HUD button in the bottom-right row, level with the caption's middle."""
    obj(name, x, y, width, 28, parent=parent, comps=f"  - type: uiRect\n    anchor: {anchor}\n    scaleHorizontalWithText: true\n  - type: uiButton\n"
        + ui_text(text, key, wrap=True) + behaviour)


def badge(name, x, y, width, flag, text, key, anchor, behaviour=""):
    """A panel that shows while a session flag is up (pause, door hint, compass)."""
    obj(name, x, y, width, 26, parent="Interface", comps=f"  - type: uiRect\n    anchor: {anchor}\n    showIf: {flag}\n" + panel_image() + ui_text(text, key) + behaviour)


def panel_image():
    return f"  - type: uiImage\n    sprite: {PANEL}\n    borders: {{left: 8, top: 8, right: 8, bottom: 8}}\n"


def controls_hud(hint, buttons_left):
    """H or the Controls button shows the bindings panel above the caption; the panel lists the current controls (ToggleControls)."""
    button("Controls button", -12, 88, "Controls", "hud.controls", script("ToggleControls", f'      Hint: "{oid("Controls hint")}"\n      CheckpointHint: "{oid("Checkpoint controls")}"\n'))
    button("Language", buttons_left, 102, "한국어 [L]", "ui.language", script("GameLanguageButton"))
    obj("Controls panel", 12, -62, 580, 40, parent="Interface", comps="  - type: uiRect\n    anchor: BottomLeft\n    showIf: controls\n" + panel_image() + "  - type: uiLayout\n    padding: 12\n    gap: 4\n")
    obj("Controls hint", 0, 0, 556, 20, parent="Controls panel", comps="  - type: uiRect\n" + ui_text(hint, size=13, inset=(0, 0), wrap=True))
    obj("Checkpoint controls", 0, 0, 556, 20, parent="Controls panel", comps="  - type: uiRect\n" + ui_text("1 / 2: Slot   F5: Save   F9: Load   M: Guide", size=13, inset=(0, 0), wrap=True, color="#BFD0DC"))


def bag_hud():
    """I opens the bag: an input-mode panel with the compass and the key quest, and a confirmation dialog on top of it."""
    button("Bag button", -106, 88, "Bag [I]", "hud.bag", script("BagMenu", f'      OpenPanel: "{oid("Inventory")}"\n      Shortcut: "OpenBag"\n'
           f'      StatusLabel: "{oid("Inventory item")}"\n      QuestLabel: "{oid("Key quest")}"\n      QuestionLabel: "{oid("Compass question")}"\n'))
    badge("Compass bearing", 14, 46, 215, "compass", "Compass  /  Exit north", "hud.bearing", "TopLeft")
    modal = lambda title, key: "    anchor: Center\n    inputModeRoot: true\n" + panel_image() + ui_text(title, key, 20, (18, 13))
    obj("Inventory", 0, 0, 320, 150, parent="Interface", comps="  - type: uiRect\n    left: 64\n    right: 64\n    top: 36\n    bottom: 36\n" + modal("Inventory", "bag.title"))
    obj("Inventory details", 0, 0, 284, 80, parent="Inventory", comps="  - type: uiRect\n    left: 18\n    right: 18\n    top: 84\n    bottom: 80\n  - type: uiLayout\n    gap: 4\n    padding: 0\n  - type: uiScroll\n")
    obj("Inventory item", 18, 54, 284, 30, parent="Inventory details", comps="  - type: uiRect\n" + ui_text("Pocket compass  /  In bag", size=15, inset=(0, 0), wrap=True))
    obj("Key quest", 18, 77, 284, 20, parent="Inventory details", comps="  - type: uiRect\n" + ui_text("", size=14, inset=(0, 0), wrap=True, color="#FFD84A"))
    corner = lambda left, right: f"  - type: uiRect\n" + (f"    left: {left}\n" if left is not None else "") + f"    right: {right}\n    bottom: 18\n  - type: uiButton\n"
    obj("Manage compass", 18, 100, 164, 32, parent="Inventory", comps=corner(18, 158) + ui_text("Manage compass", "bag.manage", 15, (12, 5))
        + script("BagMenu", f'      OpenPanel: "{oid("Compass confirmation")}"\n'))
    obj("Close inventory", 192, 100, 128, 32, parent="Inventory", comps=corner(None, 18) + ui_text("Close", "ui.close", 15, (12, 5))
        + script("BagMenu", f'      ClosePanel: "{oid("Inventory")}"\n      Shortcut: "MenuBack"\n'))
    obj("Compass confirmation", 0, 0, 320, 150, parent="Inventory", comps="  - type: uiRect\n    left: 0\n    right: 0\n    top: 0\n    bottom: 0\n"
        + modal("Confirm", "ui.confirm"))
    obj("Compass question", 18, 54, 284, 30, parent="Compass confirmation", comps="  - type: uiRect\n    left: 18\n    right: 18\n    top: 86\n"
        + ui_text("Equip the pocket compass?", size=15, inset=(0, 0), wrap=True))
    obj("Apply compass", 18, 100, 164, 32, parent="Compass confirmation", comps=corner(18, 158) + ui_text("Apply", "ui.apply", 15, (12, 5))
        + script("BagMenu", f'      ClosePanel: "{oid("Compass confirmation")}"\n      EquipCompass: true\n'))
    obj("Cancel compass", 192, 100, 128, 32, parent="Compass confirmation", comps=corner(None, 18) + ui_text("Cancel", "ui.cancel", 15, (12, 5))
        + script("BagMenu", f'      ClosePanel: "{oid("Compass confirmation")}"\n      Shortcut: "MenuBack"\n'))
    for name, parent in (("Bag language", "Inventory"), ("Confirm language", "Compass confirmation")):
        obj(name, -18, 14, 150, 28, parent=parent, comps="  - type: uiRect\n    right: 18\n    top: 14\n    anchor: TopRight\n  - type: uiButton\n"
            + ui_text("한국어", "ui.languageName", 14, (8, 4), wrap=True) + script("GameLanguageButton"))


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
# Component keys (Assets/*.clip with tracks): the closed chest rocks on its base until it is opened, and a spawned coin glints.
LID_MOTION, COIN_PULSE = "a035d2c0-22b0-4dc1-b601-fa828b99d308", "064d0c10-980c-435f-963d-969603f34ff8"
keys = lambda prop, *points: f"- property: {prop}\n  keys:\n" + "".join(f'  - time: {t:g}\n    value: "{v}"\n' for t, v in points)
(ROOT / "Assets/LidMotion.clip").write_text("frames:\n- seconds: 1\n  useRenderer: true\nloop: true\ntracks:\n" + keys("Rotation", (0, 0), (.5, -4), (1, 0))
                                          + keys("ScaleY", (0, 1), (.5, 1.06), (1, 1)) + keys("Tint", (0, "#FFFFFF")))
(ROOT / "Assets/CoinPulse.clip").write_text("frames:\n- seconds: 1\n  useRenderer: true\nloop: true\ntracks:\n" + keys("Tint", (0, "#FFFFFF"), (.5, "#FFE9A0"), (1, "#FFFFFF"))
                                          + keys("Opacity", (0, 1), (.5, .7), (1, 1)))
w, h = png_size(ART / "CoinPile.png")
# The group is LootRules.Collectibles, which C (carry a coin) looks for.
coin_prefab = text_asset("Coin.prefab", f"schemaVersion: 27\nobjects:\n- id: {uid('prefab/coin')}\n  name: Coin\n  tag: Coin\n  groups:\n  - 수집품\n  width: {w}\n  height: {h}\n  components:\n"
                         + sprite("CoinPile.png", order=2) + f"  - type: spriteAnimator\n    clip: {COIN_PULSE}\n    playOnStart: true\n" + TRIGGER
                         + script("CollectCoin", f'      LootTable:\n        Id: "{LOOT_TABLE}"\n'))
pot_link = pot_prefab()
# The satchel on the hero's hip (SatchelPop pops it when a coin goes in): a leather bag, its flap and a brass clasp.
pixels("Satchel.png", [".ooooo.", "ohhhhlo", "ollllso", "ollllso", "ossssso", ".ooooo."],
       {"o": "#3B2418", "h": "#A85E34", "l": "#8A4B2A", "s": "#6B3820"})
pixels("SatchelFlap.png", ["ooooooo", "offfffo"], {"o": "#3B2418", "f": "#B4693A"})
pixels("SatchelClasp.png", ["y"], {"y": "#FFD84A"})

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
controls_hud("Push the crate onto the plate. Stand beside it to open the door.", -106)
badge("Pause indicator", 0, 12, 116, "paused", "Paused", "hud.paused", "TopCenter", script("PauseGame"))
room2 = write("ShowcaseRoom2.scene")

# ---- room 1: key, chest, door ---------------------------------------------------------------------------------------
scene_name, out = "room1", []
room(7)
obj("Rug", 388, 222, *png_size(ART / "Rug.png"), comps=sprite("Rug.png", order=-6))
door("Door", script("DoorLock") + SOLID + sprite("Door.png", order=-5) + f"  - type: spriteAnimator\n    clip: {door_clip}\n    playOnStart: false\n  - type: audioSource\n    sound: {UNLOCK_WAV}\n"
     + script("RemoveOnAction", '      Action: "RemoveDoor"\n'))   # F6 and F7 remove the door and the key (object lifetime)
obj("Exit", DOOR_COLS[0] * T + 4, 3 * T - 16, 2 * T - 8, 12, comps=TRIGGER + script("RoomExit", f'      Player: "{oid("Player")}"\n      Destination:\n        Id: "{room2}"\n')
    + script("DoorwayFade", f'      Player: "{oid("Player")}"\n      Hide: "{oid("Hero body")}"\n      FadeOut: false\n'))   # no fade here: a fade outlives the scene change

prop("Barrel 1", "Barrel.png", 42, 124)
prop("Barrel 2", "Barrel.png", 78, 132)
prop("Crate", "Crate.png", 444, 126, drag="Tag 4")      # both crates follow a mouse drag (world pointer)
prop("Pot 1", "Pot.png", 482, 136)
# Two instances of Pot.prefab: "Pot" as authored, "Small pot" overriding its name, size and picture.
pot("Pot", "Pot.png", 44, 250, "ShadowL.png", pot_link)
pot("Small pot", "PotSmall.png", 84, 258, "ShadowM.png", pot_link)
prop("Crate 2", "Crate.png", 452, 286, drag="Tag 4")
prop("Barrel 3", "Barrel.png", 494, 288)

# Chest: the body has no picture. "Chest Lid" (the closed chest) is what OpenChest hides; "Chest open" starts off and
# FlagFx switches it on with the chest flag. Both stand bottom-centred in the body box.
ow, oh = png_size(ART / "ChestOpen.png")
lw, lh = png_size(ART / "Chest.png")
cw2, ch2, cx, cb = max(ow, lw), max(oh, lh), 364, 214
shade("Chest", "ShadowL.png", cx, cb, cw2)
obj("Chest", cx, cb - ch2, cw2, ch2, comps=GROUP + script("OpenChest",
    f'      Player: "{oid("Player")}"\n      Lid: "{oid("Chest Lid")}"\n      Rules:\n        Id: "{RULES}"\n      SaveProgress: true\n      Loot:\n        Id: "{coin_prefab}"\n'))
obj("Chest Lid", (cw2 - lw) / 2, ch2 - lh, lw, lh, parent="Chest", comps=sprite("Chest.png", order=1) + f"  - type: spriteAnimator\n    clip: {LID_MOTION}\n    playOnStart: true\n",
    extra="  pivotX: 0.5\n  pivotY: 1\n")   # rocks on its base
obj("Chest open", (cw2 - ow) / 2, ch2 - oh, ow, oh, parent="Chest", comps=sprite("ChestOpen.png", order=1), extra="  active: false\n")
obj("Chest base", 2, ch2 - 14, cw2 - 4, 12, parent="Chest", comps=SOLID)
obj("Chest sparkle", cw2 / 2, ch2 / 2, 1, 1, parent="Chest", comps=emitter(sparkle, "manual", layer=5))

# Key: the trigger stays; KeySequence hides the picture (and with it the glow and the shadow).
kw, kh = png_size(ART / "Key.png")
obj("Key", 168, 186, kw, kh, comps=TRIGGER + f"  - type: script\n    script: KeySequence\n    eventLinks:\n    - event: KeyAcquired\n      target: {oid('Door')}\n      script: DoorLock\n      method: Unlock\n    values:\n"
    f'      Collector: "{oid("Player")}"\n      KeyImage: "{oid("Key Image")}"\n      Frames:\n      - "{asset("DoorOpen.png")}"\n      DoorCamera: "{oid("Door Camera")}"\n      PlayerCamera: "{oid("Main Camera")}"\n      HoldSeconds: 1.4\n'
    + script("RemoveOnAction", '      Action: "RemoveKey"\n'))
obj("Key Image", 0, 0, kw, kh, parent="Key", comps=sprite("Key.png", sortByY="true") + light(44, 0.5, 0.35, 1.1, "#FFE27A"))
sw, sh = png_size(ART / "ShadowM.png")
obj("Key shade", (kw - sw) / 2, kh - sh * .4, sw, sh, parent="Key Image", comps=sprite("ShadowM.png", order=-4))

hero(277, 196)
# The satchel hangs on the hero's hip inside his sorting group.
obj("Satchel", 16, -12, 14, 12, parent="Player", comps=sprite("Satchel.png", order=20) + script("SatchelPop"))
obj("Satchel flap", 0, 0, 14, 4, parent="Satchel", comps=sprite("SatchelFlap.png", order=21))
obj("Satchel clasp", 6, 3, 2, 2, parent="Satchel", comps=sprite("SatchelClasp.png", order=22))
obj("Main Camera", COLS * T / 2, ROWS * T / 2, 40, 40, comps="  - type: camera2D\n    zoom: 1.5\n" + script("DetailView", f'      Camera: "{oid("Detail Camera")}"\n'))
obj("Door Camera", COLS * T / 2, 2.6 * T, 40, 40, comps="  - type: camera2D\n    zoom: 3\n")
# F8: a second camera drawn into an inset viewport over the chest (camera output Extra).
obj("Detail Camera", 384, 200, 40, 40, extra="  active: false\n",
    comps="  - type: camera2D\n    output: Extra\n    zoom: 2.5\n    viewport: {x: 0.02, y: 0.2, width: 0.25, height: 0.32}\n")
obj("Door dust", COLS * T / 2, 3 * T - 6, 1, 1, comps=emitter(door_dust, "manual", layer=5))
fx("Door feedback", "key", emit="Door dust", shake=3, delay=DOOR_DELAY)
fx("Chest feedback", "chest", emit="Chest sparkle", punch=0.05, show="Chest open")
hud("A small game in a handful of scripts", "Controls", "Move  WASD      Open  E      Bag  I      Help  H",
    [("key", "Event link", "Key → Door.Unlock"), ("chest", "Prefab spawn", "Chest → Coins"), ("coin", "Trigger pickup", "Coin → HUD"),
     ("dragged", "Mouse drag", "World pointer → crate"), ("detail", "Second camera", "F8 inset viewport")])
controls_hud("Move: arrows / WASD   Chest: E", -200)
bag_hud()
badge("Door hint", 0, 12, 280, "doorHint", "The door is open. Head north.", "hud.door", "TopCenter")
badge("Pause indicator", 0, 44, 116, "paused", "Paused", "hud.paused", "TopCenter", script("PauseGame"))
write("Showcase.scene")
