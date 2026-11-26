# Writes CollisionLab's showcase: Scenes/ShowcaseLantern.scene (night field, lantern, HUD) and Scenes/ShowcaseStones.scene
# (10,000 stone colliders the lantern scene adds with Session.AddSceneAsync). Run after art.py:  python3 showcase-src/collision/scene.py
#
# LanternQuery (Scripts/Showcase) moves the lantern, streams the stones in on L, then asks one overlap query per fixed step
# through the spatial grid and once more as a full scan; the grid's answer is lit, the panel compares the two.
# The field is dark (Global Light 2D); the lantern carries a Point Light 2D and draws, with its ring, on the unlit layer.
# Stones keep their positions and colliders (same random sequence); a second sequence only picks one of three looks per kind,
# and each look is a group (Stone0..Stone8) whose dim and lit sprites LanternQuery swaps.
import pathlib, random, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/CollisionLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("5750e5bb-1700-5a5b-94c2-b6bd02a0356c")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


def scene_id(file):
    meta = ROOT / "Scenes" / (file + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid('scene/' + file)}\n")
    return meta.read_text().split()[1]


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None, extra=""):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n{extra}"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
label = lambda t, size=12, color="#F4F6F4": f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n'

# ---- stones: 125 × 80 = 10,000 on a jittered grid over the whole view ------------------------------------------------
KINDS = [("Pebble", "  - {type: circleCollider2D}\n"), ("Block", "  - {type: boxCollider2D}\n"),
         ("Shard", "  - {type: polygonCollider2D, points: [{x: 0, y: 0}, {x: 1, y: 0.5}, {x: 0, y: 1}]}\n")]
LOOKS = [f"{kind}{i}" for kind, _ in KINDS for i in (1, 2, 3)]   # group Stone<k> draws LOOKS[k]
rng, looks = random.Random(10000), random.Random(3)
stones = ["schemaVersion: 42\nobjects:\n"]
n = 0
for row in range(80):
    for col in range(125):
        n += 1
        kind = 0 if (k := rng.random()) < .5 else 1 if k < .75 else 2
        look = kind * 3 + looks.randrange(3)
        x, y = round(col * 6.4 + 1.5 + rng.uniform(-1, 1)), round(row * 5.625 + 1 + rng.uniform(-.8, .8))
        stones.append(f"- id: {uid(f'stone/{n}')}\n  name: Stone {n}\n  groups: [Stone{look}]\n  x: {x}\n  y: {y}\n  width: 5\n  height: 5\n"
                      f"  components:\n{KINDS[kind][1]}" + sprite(LOOKS[look] + ".png"))
(ROOT / "Scenes/ShowcaseStones.scene").write_text("".join(stones))
stones_scene = scene_id("ShowcaseStones.scene")

# ---- lantern scene ----------------------------------------------------------------------------------------------------
s = Scene("lantern")
s.obj("Night", 0, 0, 1, 1, '  - type: globalLight2D\n    color: "#5A6280"\n    unlitLayer: 5\n')
s.obj("Ground", 0, 0, 800, 450, sprite("Field.png", sortingLayer=-10))
# Edges of the field: a well and bushes above, a fence below, bushes and flowers at the sides, all clear of the lantern's path
# (x 82..718, y 137..363) so no lit stone hides under them. They draw over the stones (layer 1).
props = [("Well", "Well.png", 367, 2, 66, 80), ("Bush top", "Bush.png", 442, 44, 50, 42), ("Flowers top", "Flowers.png", 312, 98, 54, 32),
         ("Bush left", "Bush.png", -16, 176, 50, 42), ("Flowers left", "Flowers.png", 4, 292, 54, 32), ("Bush left low", "Bush.png", -10, 336, 50, 42),
         ("Bush right", "Bush.png", 762, 150, 50, 42), ("Flowers right", "Flowers.png", 744, 238, 54, 32), ("Bush right low", "Bush.png", 756, 310, 50, 42),
         ("Bush bottom", "Bush.png", 560, 384, 50, 42), ("Flowers bottom", "Flowers.png", 652, 396, 54, 32)]
for name, image, x, y, w, h in props:
    s.obj(name + " shadow", x + w / 2 - 16, y + h - 6, 32, 10, sprite("ShadowM.png", sortingLayer=1, order=-1))
    s.obj(name, x, y, w, h, sprite(image, sortingLayer=1))
s.obj("Fence", 0, 418, 816, 34, sprite("Fence.png", drawMode="Tiled", sortingLayer=1, order=2))
lantern = s.obj("Lantern", 360, 185, 80, 80, sprite("LanternRing.png", sortingLayer=5)
                + '  - type: pointLight2D\n    radius: 124\n    intensity: 1\n    flicker: 0.12\n    frequency: 1.3\n    color: "#FFC46A"\n')
s.obj("Lamp", 24, 16, 32, 32, sprite("Lantern.png", sortingLayer=5, order=1), parent="Lantern")
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")
# Query panel, top right: three rows on shared columns.
PW = 286
s.obj("Query panel", -12, 12, PW, 118, "  - type: uiRect\n    anchor: TopRight\n" + panel(), parent="Canvas")
s.obj("Query title", 12, 8, PW - 24, 16, label("ONE OVERLAP QUERY PER FRAME", 11, "#BFD0DC"), parent="Query panel")
s.obj("Hits", 12, 24, PW - 24, 22, label("Press L to load the field", 15, "#FFD84A"), parent="Query panel")
for i, (key, name) in enumerate((("Grid", "Spatial grid"), ("Scan", "Full scan"))):
    y = 52 + i * 20
    s.obj(f"{key} name", 12, y, 96, 16, label(name, 12, "#BFD0DC"), parent="Query panel")
    s.obj(f"{key} checked", 108, y, 100, 16, label("-", 12), parent="Query panel")
    s.obj(f"{key} time", 208, y, 66, 16, label("-", 12), parent="Query panel")
s.obj("Verdict", 12, 94, PW - 24, 16, label("", 12, "#9FE6A0"), parent="Query panel")
# Loading panel, centered: status text and a bar whose fill grows with the operation's progress.
LW = 360
s.obj("Load panel", 0, 0, LW, 62, "  - type: uiRect\n    anchor: Center\n" + panel(), parent="Canvas")
s.obj("Load text", 14, 10, LW - 28, 18, label("10,000 colliders are ready to load.   L", 13), parent="Load panel")
s.obj("Load bar", 14, 34, LW - 28, 16, "  - type: uiRect\n" + panel(), parent="Load panel")
s.obj("Load fill", 3, 3, 1, 10, f"  - type: uiRect\n  - type: uiImage\n    sprite: {asset('BarFill.png')}\n", parent="Load bar")
tags = [("loaded", "Split loading", "10,000 colliders stream in, 0.5 ms per frame"), ("grid", "Spatial grid", "A query checks only nearby cells"),
        ("match", "Same answers", "Grid and full scan agree every frame")]
tag_w = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
title = "10,000 colliders, one query each frame"
cw = 110 + 7.4 * len(title)
s.obj("Caption", 12, -12, cw, 44, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption title", 12, 5, cw - 24, 20, label(f"<b>CollisionLab</b>   {title}", 15), parent="Caption")
s.obj("Caption controls", 12, 25, cw - 24, 16, label("Load the field  L", 12, "#BFD0DC"), parent="Caption")
refs = {k: "[" + ", ".join(f'"{asset(look + suffix + ".png")}"' for look in LOOKS) + "]" for k, suffix in (("Dim", ""), ("Lit", "Lit"))}
values = {"Stones": f'{{"Id": "{stones_scene}"}}', "Lantern": f'"{lantern}"'}
values.update({k: f'"{s.oid(k)}"' for k in ("Hits", "Grid checked", "Grid time", "Scan checked", "Scan time", "Verdict", "Load panel", "Load text", "Load fill")})
values = {"".join(w[0].upper() + w[1:] for w in k.split()): v for k, v in values.items()} | refs
s.obj("Field", 0, 0, 1, 1, "  - type: script\n    script: Showcase/LanternQuery\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseLantern.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
print("lantern", scene_id("ShowcaseLantern.scene"), "stones", stones_scene, n)
