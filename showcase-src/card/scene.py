# Writes CardLab's showcase: Scenes/ShowcaseTrail.scene and Assets/Showcase/ShowcaseTrail.table. Run after art.py:
#   python3 showcase-src/card/scene.py
#
# A trail of 25 stepping stones across a yard, a card table below. CardTrail (Scripts/Showcase) keeps the sample's CardDeck
# (seeded numbered shuffles, draw / hand / discard piles, Capture / Restore) and the steps column of the table; playing a card
# walks the hero that many stones. Card faces are UI: a 9-slice frame and rich text with the card's <icon=…>.
import math, pathlib, struct, uuid

ROOT = pathlib.Path(__file__).resolve().parents[2] / "samples/CardLab"
ART = ROOT / "Assets/Showcase"
NS = uuid.UUID("88c5afa9-de97-52e8-bc21-2f5b0094b98c")
uid = lambda name: str(uuid.uuid5(NS, "showcase/" + name))


def png_size(name):
    return struct.unpack(">II", (ART / name).read_bytes()[16:24])


def asset(name):
    meta = ART / (name + ".meta")
    if not meta.exists(): meta.write_text(f"id: {uid(name)}\n")
    return meta.read_text().split()[1]


class Scene:
    def __init__(self, key):
        self.key, self.out = key, []
    def oid(self, name): return uid(f"obj/{self.key}/{name}")
    def obj(self, name, x, y, w, h, comps="", parent=None):
        self.out.append(f"- id: {self.oid(name)}\n" + (f"  parentId: {self.oid(parent)}\n" if parent else "") + f"  name: {name}\n"
                        f"  x: {x:g}\n  y: {y:g}\n  width: {w:g}\n  height: {h:g}\n" + (f"  components:\n{comps}" if comps else "  components: []\n"))
        return self.oid(name)


def sprite(image, **kw):
    return f"  - type: spriteRenderer\n    assetId: {asset(image)}\n    asset: Assets/Showcase/{image}\n" + "".join(f"    {k}: {v}\n" for k, v in kw.items())


panel = lambda: f"  - type: uiImage\n    sprite: {asset('Panel.png')}\n    borders: {{left: 4, top: 4, right: 4, bottom: 4}}\n"
CARDS = [("herb", "Herb", "Forage", 1), ("bread", "Bread", "Rest stop", 1), ("lantern", "Lantern", "Night walk", 2),
         ("compass", "Compass", "Bearing", 2), ("map", "Map", "Shortcut", 3), ("rope", "Rope", "Climb", 3)]
icons = "    icons: {" + ", ".join(f"{key}: {asset(png + '.png')}" for key, png, _, _ in CARDS) + "}\n"


def label(t, size=12, color="#F4F6F4", extra=""):
    return f'  - type: uiRect\n  - type: uiText\n    richText: true\n    text: "{t}"\n    size: {size}\n    color: "{color}"\n{extra}'


# ---- data table: the same columns as the sample's Trail.table, English labels, one copy each -------------------------
table = ("version: 1\ncolumns:\n- name: label\n  type: Text\n- name: copies\n  type: Number\n- name: steps\n  type: Number\nrows:\n"
         + "".join(f'- key: {key}\n  cells: ["{name}", 1, {steps}]\n' for key, _, name, steps in CARDS))
(ART / "ShowcaseTrail.table").write_text(table)
table_id = asset("ShowcaseTrail.table")

s = Scene("trail")
MAP_H = 262
s.obj("Grass", 0, 0, 800, MAP_H, sprite("Grass.png", drawMode="Tiled", order=-20))
s.obj("Felt", 0, MAP_H, 800, 450 - MAP_H, sprite("Felt.png", drawMode="Tiled", order=-20))
s.obj("Rail", 0, MAP_H - 3, 800, 6, sprite("Rail.png", drawMode="Tiled", order=-19))
# Trail: 25 stones (steps 0..24); CardTrail uses the same formula for the hero's targets.
STEPS = 24
stone = lambda i: (70 + i * 27.5, 178 + 34 * math.sin(i * .55))
for i in range(STEPS + 1):
    cx, cy = stone(i)
    s.obj(f"Stone {i}", round(cx - 12), round(cy - 7), 24, 14, sprite("Stone.png", order=-5))
gx, gy = stone(STEPS)
cw, ch = png_size("Chest.png")
s.obj("Camp chest", round(gx + 14), round(gy - ch + 6), cw, ch, sprite("Chest.png", order=round(gy)))
for name, image, x, y in (("Bush A", "Bush.png", 140, 92), ("Flowers A", "Flowers.png", 292, 214), ("Fence", "Fence.png", 420, 104),
                          ("Barrel", "Barrel.png", 528, 206), ("Flowers B", "Flowers.png", 610, 92), ("Bush B", "Bush.png", 24, 200)):
    w, h = png_size(image)
    s.obj(name, x, y, w, h, sprite(image, order=y + h))
hx, hy = stone(0)
hw, hh = png_size("HeroIdleE.png")
s.obj("Hero shadow", round(hx - 16), round(hy - 6), 32, 10, sprite("ShadowM.png", order=290))
s.obj("Hero", round(hx - hw / 2), round(hy - hh + 4), hw, hh, sprite("HeroIdleE.png", order=300))
s.obj("Camera", 400, 225, 1, 1, "  - type: camera2D\n")
s.obj("Canvas", 0, 0, 1, 1, "  - type: uiCanvas\n")

# Table UI: draw pile, six hand slots, discard pile. Cards are 84 × 116 with 8 between hand slots.
CW, CH, GAP, CY = 84, 116, 8, 292
HX, DX, XX = 122, 18, 698
s.obj("Draw label", DX, CY - 22, CW + 20, 16, label("DRAW  6", 12, "#BFD0DC"), parent="Canvas")
s.obj("Discard label", XX, CY - 22, CW + 20, 16, label("DISCARD  0", 12, "#BFD0DC"), parent="Canvas")
s.obj("Log", HX, CY - 22, 6 * CW + 5 * GAP, 16, label("", 12, "#BFD0DC"), parent="Canvas")
for k in range(3):                             # a small stack of backs
    s.obj(f"Draw back {k + 1}", DX + 4 - 2 * k, CY + 4 - 2 * k, CW, CH, f"  - type: uiRect\n  - type: uiImage\n    sprite: {asset('CardBack.png')}\n", parent="Canvas")
s.obj("Discard slot", XX, CY, CW, CH, "  - type: uiRect\n" + panel(), parent="Canvas")


def card(name, x):   # CardTrail hides empty slots on its first refresh
    s.obj(name, x, CY, CW, CH, "  - type: uiRect\n"
          + f"  - type: uiImage\n    sprite: {asset('Card.png')}\n    borders: {{left: 6, top: 6, right: 6, bottom: 6}}\n", parent="Canvas")
    s.obj(f"{name} steps", 9, 6, 40, 20, label("+1", 16, "#7A4A14"), parent=name)
    s.obj(f"{name} icon", 10, 24, 64, 66, label("<icon=herb>", 64, "#FFFFFF", icons), parent=name)
    s.obj(f"{name} name", 9, 92, CW - 14, 16, label("Forage", 12, "#2B2118"), parent=name)
    return [s.oid(n) for n in (name, f"{name} icon", f"{name} name", f"{name} steps")]


slots = [card(f"Card {i + 1}", HX + i * (CW + GAP)) for i in range(6)]
for i in range(6): s.obj(f"Card {i + 1} key", CW - 18, 6, 12, 18, label(str(i + 1), 12, "#9C8A66"), parent=f"Card {i + 1}")
discard = card("Discard card", XX)

s.obj("Info panel", -12, 12, 268, 64, "  - type: uiRect\n    anchor: TopRight\n" + panel(), parent="Canvas")
s.obj("Info", 12, 5, 244, 18, label("Seed 42     Shuffle 1     Step 0 / 24", 13, "#FFD84A"), parent="Info panel")
s.obj("Note", 12, 24, 244, 16, label("Draw cards and play them to walk.", 12, "#BFD0DC"), parent="Info panel")
s.obj("Last", 12, 42, 244, 16, label("", 12, "#9FE6A0"), parent="Info panel")
# In the order they light up during the run.
tags = [("table", "Cards from a data table", "Names and steps in one .table"), ("save", "Exact save", "Every pile and the hero come back"),
        ("seed", "Seeded shuffle", "Same seed, same order")]
tag_w = 24 + 6.2 * max(len(f) + len(d) + 3 for _, f, d in tags)
for i, (flag, feature, detail) in enumerate(tags):
    s.obj(f"Tag {i + 1}", 12, 12 + i * 28, tag_w, 24, f"  - type: uiRect\n    anchor: TopLeft\n    showIf: {flag}\n" + panel()
          + f'  - type: uiText\n    richText: true\n    text: "<b>{feature}</b>   <color=#BFD0DC>{detail}</color>"\n    size: 12\n    insetX: 10\n    insetY: 4\n', parent="Canvas")
caption = "<b>CardLab</b>   A seeded deck with exact saves       <color=#BFD0DC>Draw D    Play 1-6    Save S    Load L    Restart R</color>"
s.obj("Caption", 12, -6, 600, 26, "  - type: uiRect\n    anchor: BottomLeft\n" + panel(), parent="Canvas")
s.obj("Caption text", 12, 4, 580, 18, label(caption, 13), parent="Caption")

ref = lambda n: f'{{"Id": "{asset(n)}"}}'
deck = s.oid("Deck")
values = {"Source": f'"{deck}"', "Hero": f'"{s.oid("Hero")}"', "Shadow": f'"{s.oid("Hero shadow")}"', "Info": f'"{s.oid("Info")}"',
          "Note": f'"{s.oid("Note")}"', "Last": f'"{s.oid("Last")}"', "Log": f'"{s.oid("Log")}"', "DrawLabel": f'"{s.oid("Draw label")}"', "DiscardLabel": f'"{s.oid("Discard label")}"',
          "DrawBacks": "[" + ", ".join(f'"{s.oid(f"Draw back {k + 1}")}"' for k in range(3)) + "]",
          "Cards": "[" + ", ".join(f'"{c[0]}"' for c in slots) + "]", "Icons": "[" + ", ".join(f'"{c[1]}"' for c in slots) + "]",
          "Names": "[" + ", ".join(f'"{c[2]}"' for c in slots) + "]", "Steps": "[" + ", ".join(f'"{c[3]}"' for c in slots) + "]",
          "Discard": f'"{discard[0]}"', "DiscardIcon": f'"{discard[1]}"', "DiscardName": f'"{discard[2]}"', "DiscardSteps": f'"{discard[3]}"',
          "Idle": ref("HeroIdleE.png")} | {f"Walk{i}": ref(f"HeroWalkE{i}.png") for i in range(1, 5)}
s.obj("Deck", 0, 0, 1, 1, f'  - type: script\n    script: Decks/DeckSource\n    values: {{"Catalog": {{"Id": "{table_id}"}}, "InitialSeed": 42}}\n'
      "  - type: script\n    script: Showcase/CardTrail\n    values: {" + ", ".join(f'"{k}": {v}' for k, v in values.items()) + "}\n")
(ROOT / "Scenes/ShowcaseTrail.scene").write_text("schemaVersion: 42\nobjects:\n" + "".join(s.out) + f"defaultCameraId: {s.oid('Camera')}\n")
meta = ROOT / "Scenes/ShowcaseTrail.scene.meta"
if not meta.exists(): meta.write_text(f"id: {uid('scene/ShowcaseTrail.scene')}\n")
print("scene", meta.read_text().split()[1], "table", table_id)
